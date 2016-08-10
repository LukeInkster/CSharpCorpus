// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.AspNetCore.Routing.Tree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.ObjectPool;
using Moq;
using Xunit;

namespace Microsoft.AspNetCore.Mvc.Internal
{
    public class AttributeRouteTest
    {
        private static readonly RequestDelegate NullHandler = (c) => Task.FromResult(0);

        // This test verifies that AttributeRoute can respond to changes in the AD collection. It does this
        // by running a successful request, then removing that action and verifying the next route isn't
        // successful.
        [Fact]
        public async Task AttributeRoute_UsesUpdatedActionDescriptors()
        {
            // Arrange
            ActionDescriptor selected = null;

            var actions = new List<ActionDescriptor>()
            {
                new ActionDescriptor()
                {
                    AttributeRouteInfo = new AttributeRouteInfo()
                    {
                        Template = "api/Blog/{key1}"
                    },
                },
                new ActionDescriptor()
                {
                    AttributeRouteInfo = new AttributeRouteInfo()
                    {
                        Template = "api/Store/Buy/{key2}"
                    },
                },
            };


            Func<ActionDescriptor[], IRouter> handlerFactory = (_) =>
            {
                var handler = new Mock<IRouter>();
                handler
                    .Setup(r => r.RouteAsync(It.IsAny<RouteContext>()))
                    .Returns<RouteContext>(routeContext =>
                    {
                        if (routeContext.RouteData.Values.ContainsKey("key1"))
                        {
                            selected = actions[0];
                        }
                        else if (routeContext.RouteData.Values.ContainsKey("key2"))
                        {
                            selected = actions[1];
                        }

                        routeContext.Handler = (c) => TaskCache.CompletedTask;

                        return TaskCache.CompletedTask;

                    });
                return handler.Object;
            };

            var actionDescriptorProvider = CreateActionDescriptorProvider(actions);
            var route = CreateRoute(handlerFactory, actionDescriptorProvider.Object);

            var requestServices = new Mock<IServiceProvider>(MockBehavior.Strict);
            requestServices
                .Setup(s => s.GetService(typeof(ILoggerFactory)))
                .Returns(NullLoggerFactory.Instance);

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Path = new PathString("/api/Store/Buy/5");
            httpContext.RequestServices = requestServices.Object;

            var context = new RouteContext(httpContext);

            // Act 1
            await route.RouteAsync(context);

            // Assert 1
            Assert.NotNull(context.Handler);
            Assert.Equal("5", context.RouteData.Values["key2"]);
            Assert.Same(actions[1], selected);
            
            // Arrange 2 - remove the action and update the collection
            selected = null;
            actions.RemoveAt(1);
            actionDescriptorProvider
                .SetupGet(ad => ad.ActionDescriptors)
                .Returns(new ActionDescriptorCollection(actions, version: 2));

            context = new RouteContext(httpContext);

            // Act 2
            await route.RouteAsync(context);

            // Assert 2
            Assert.Null(context.Handler);
            Assert.Empty(context.RouteData.Values);
            Assert.Null(selected);
        }

        [Fact]
        public void AttributeRoute_GetEntries_CreatesOutboundEntry()
        {
            // Arrange
            var actions = new List<ActionDescriptor>()
            {
                new ActionDescriptor()
                {
                    AttributeRouteInfo = new AttributeRouteInfo()
                    {
                        Template = "api/Blog/{id}",
                        Name = "BLOG_INDEX",
                        Order = 17,
                    },
                    RouteValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "controller", "Blog" },
                        { "action", "Index" },
                    },
                },
            };

            var builder = CreateBuilder();
            var actionDescriptorProvider = CreateActionDescriptorProvider(actions);
            var route = CreateRoute(CreateHandler().Object, actionDescriptorProvider.Object);

            // Act
            route.AddEntries(builder, actionDescriptorProvider.Object.ActionDescriptors);

            // Assert
            Assert.Collection(
                builder.OutboundEntries,
                e =>
                {
                    Assert.Empty(e.Constraints);
                    Assert.Empty(e.Defaults);
                    Assert.Equal(RoutePrecedence.ComputeOutbound(e.RouteTemplate), e.Precedence);
                    Assert.Equal("BLOG_INDEX", e.RouteName);
                    Assert.Equal(17, e.Order);
                    Assert.Equal(ToRouteValueDictionary(actions[0].RouteValues), e.RequiredLinkValues);
                    Assert.Equal("api/Blog/{id}", e.RouteTemplate.TemplateText);
                });
        }

        [Fact]
        public void AttributeRoute_GetEntries_CreatesOutboundEntry_WithConstraint()
        {
            // Arrange
            var actions = new List<ActionDescriptor>()
            {
                new ActionDescriptor()
                {
                    AttributeRouteInfo = new AttributeRouteInfo()
                    {
                        Template = "api/Blog/{id:int}",
                        Name = "BLOG_INDEX",
                        Order = 17,
                    },
                    RouteValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "controller", "Blog" },
                        { "action", "Index" },
                    },
                },
            };

            var builder = CreateBuilder();
            var actionDescriptorProvider = CreateActionDescriptorProvider(actions);
            var route = CreateRoute(CreateHandler().Object, actionDescriptorProvider.Object);

            // Act
            route.AddEntries(builder, actionDescriptorProvider.Object.ActionDescriptors);

            // Assert
            Assert.Collection(
                builder.OutboundEntries,
                e =>
                {
                    Assert.Single(e.Constraints, kvp => kvp.Key == "id");
                    Assert.Empty(e.Defaults);
                    Assert.Equal(RoutePrecedence.ComputeOutbound(e.RouteTemplate), e.Precedence);
                    Assert.Equal("BLOG_INDEX", e.RouteName);
                    Assert.Equal(17, e.Order);
                    Assert.Equal(ToRouteValueDictionary(actions[0].RouteValues), e.RequiredLinkValues);
                    Assert.Equal("api/Blog/{id:int}", e.RouteTemplate.TemplateText);
                });
        }

        [Fact]
        public void AttributeRoute_GetEntries_CreatesOutboundEntry_WithDefault()
        {
            // Arrange
            var actions = new List<ActionDescriptor>()
            {
                new ActionDescriptor()
                {
                    AttributeRouteInfo = new AttributeRouteInfo()
                    {
                        Template = "api/Blog/{*slug=hello}",
                        Name = "BLOG_INDEX",
                        Order = 17,
                    },
                    RouteValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "controller", "Blog" },
                        { "action", "Index" },
                    },
                },
            };

            var builder = CreateBuilder();
            var actionDescriptorProvider = CreateActionDescriptorProvider(actions);
            var route = CreateRoute(CreateHandler().Object, actionDescriptorProvider.Object);

            // Act
            route.AddEntries(builder, actionDescriptorProvider.Object.ActionDescriptors);

            // Assert
            Assert.Collection(
                builder.OutboundEntries,
                e =>
                {
                    Assert.Empty(e.Constraints);
                    Assert.Equal(new RouteValueDictionary(new { slug = "hello" }), e.Defaults);
                    Assert.Equal(RoutePrecedence.ComputeOutbound(e.RouteTemplate), e.Precedence);
                    Assert.Equal("BLOG_INDEX", e.RouteName);
                    Assert.Equal(17, e.Order);
                    Assert.Equal(ToRouteValueDictionary(actions[0].RouteValues), e.RequiredLinkValues);
                    Assert.Equal("api/Blog/{*slug=hello}", e.RouteTemplate.TemplateText);
                });
        }

        // These actions seem like duplicates, but this is a real case that can happen where two different
        // actions define the same route info. Link generation happens based on the action name + controller
        // name.
        [Fact]
        public void AttributeRoute_GetEntries_CreatesOutboundEntry_ForEachAction()
        {
            // Arrange
            var actions = new List<ActionDescriptor>()
            {
                new ActionDescriptor()
                {
                    AttributeRouteInfo = new AttributeRouteInfo()
                    {
                        Template = "api/Blog/{id}",
                        Name = "BLOG_INDEX",
                        Order = 17,
                    },
                    RouteValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "controller", "Blog" },
                        { "action", "Index" },
                    },
                },
                new ActionDescriptor()
                {
                    AttributeRouteInfo = new AttributeRouteInfo()
                    {
                        Template = "api/Blog/{id}",
                        Name = "BLOG_INDEX",
                        Order = 17,
                    },
                    RouteValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "controller", "Blog" },
                        { "action", "Index2" },
                    },
                },
            };

            var builder = CreateBuilder();
            var actionDescriptorProvider = CreateActionDescriptorProvider(actions);
            var route = CreateRoute(CreateHandler().Object, actionDescriptorProvider.Object);

            // Act
            route.AddEntries(builder, actionDescriptorProvider.Object.ActionDescriptors);

            // Assert
            Assert.Collection(
                builder.OutboundEntries,
                e =>
                {
                    Assert.Empty(e.Constraints);
                    Assert.Empty(e.Defaults);
                    Assert.Equal(RoutePrecedence.ComputeOutbound(e.RouteTemplate), e.Precedence);
                    Assert.Equal("BLOG_INDEX", e.RouteName);
                    Assert.Equal(17, e.Order);
                    Assert.Equal(ToRouteValueDictionary(actions[0].RouteValues), e.RequiredLinkValues);
                    Assert.Equal("api/Blog/{id}", e.RouteTemplate.TemplateText);
                },
                e =>
                {
                    Assert.Empty(e.Constraints);
                    Assert.Empty(e.Defaults);
                    Assert.Equal(RoutePrecedence.ComputeOutbound(e.RouteTemplate), e.Precedence);
                    Assert.Equal("BLOG_INDEX", e.RouteName);
                    Assert.Equal(17, e.Order);
                    Assert.Equal(ToRouteValueDictionary(actions[1].RouteValues), e.RequiredLinkValues);
                    Assert.Equal("api/Blog/{id}", e.RouteTemplate.TemplateText);
                });
        }

        [Fact]
        public void AttributeRoute_GetEntries_CreatesInboundEntry()
        {
            // Arrange
            var actions = new List<ActionDescriptor>()
            {
                new ActionDescriptor()
                {
                    AttributeRouteInfo = new AttributeRouteInfo()
                    {
                        Template = "api/Blog/{id}",
                        Name = "BLOG_INDEX",
                        Order = 17,
                    },
                    RouteValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "controller", "Blog" },
                        { "action", "Index" },
                    },
                },
            };

            var builder = CreateBuilder();
            var actionDescriptorProvider = CreateActionDescriptorProvider(actions);
            var route = CreateRoute(CreateHandler().Object, actionDescriptorProvider.Object);

            // Act
            route.AddEntries(builder, actionDescriptorProvider.Object.ActionDescriptors);

            // Assert
            Assert.Collection(
                builder.InboundEntries,
                e =>
                {
                    Assert.Empty(e.Constraints);
                    Assert.Equal(17, e.Order);
                    Assert.Equal(RoutePrecedence.ComputeInbound(e.RouteTemplate), e.Precedence);
                    Assert.Equal("BLOG_INDEX", e.RouteName);
                    Assert.Equal("api/Blog/{id}", e.RouteTemplate.TemplateText);
                    Assert.Empty(e.Defaults);
                });
        }

        [Fact]
        public void AttributeRoute_GetEntries_CreatesInboundEntry_WithConstraint()
        {
            // Arrange
            var actions = new List<ActionDescriptor>()
            {
                new ActionDescriptor()
                {
                    AttributeRouteInfo = new AttributeRouteInfo()
                    {
                        Template = "api/Blog/{id:int}",
                        Name = "BLOG_INDEX",
                        Order = 17,
                    },
                    RouteValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "controller", "Blog" },
                        { "action", "Index" },
                    },
                },
            };

            var builder = CreateBuilder();
            var actionDescriptorProvider = CreateActionDescriptorProvider(actions);
            var route = CreateRoute(CreateHandler().Object, actionDescriptorProvider.Object);

            // Act
            route.AddEntries(builder, actionDescriptorProvider.Object.ActionDescriptors);

            // Assert
            Assert.Collection(
                builder.InboundEntries,
                e =>
                {
                    Assert.Single(e.Constraints, kvp => kvp.Key == "id");
                    Assert.Equal(17, e.Order);
                    Assert.Equal(RoutePrecedence.ComputeInbound(e.RouteTemplate), e.Precedence);
                    Assert.Equal("BLOG_INDEX", e.RouteName);
                    Assert.Equal("api/Blog/{id:int}", e.RouteTemplate.TemplateText);
                    Assert.Empty(e.Defaults);
                });
        }

        [Fact]
        public void AttributeRoute_GetEntries_CreatesInboundEntry_WithDefault()
        {
            // Arrange
            var actions = new List<ActionDescriptor>()
            {
                new ActionDescriptor()
                {
                    AttributeRouteInfo = new AttributeRouteInfo()
                    {
                        Template = "api/Blog/{*slug=hello}",
                        Name = "BLOG_INDEX",
                        Order = 17,
                    },
                    RouteValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "controller", "Blog" },
                        { "action", "Index" },
                    },
                },
            };

            var builder = CreateBuilder();
            var actionDescriptorProvider = CreateActionDescriptorProvider(actions);
            var route = CreateRoute(CreateHandler().Object, actionDescriptorProvider.Object);

            // Act
            route.AddEntries(builder, actionDescriptorProvider.Object.ActionDescriptors);

            // Assert
            Assert.Collection(
                builder.InboundEntries,
                e =>
                {
                    Assert.Empty(e.Constraints);
                    Assert.Equal(17, e.Order);
                    Assert.Equal(RoutePrecedence.ComputeInbound(e.RouteTemplate), e.Precedence);
                    Assert.Equal("BLOG_INDEX", e.RouteName);
                    Assert.Equal("api/Blog/{*slug=hello}", e.RouteTemplate.TemplateText);
                    Assert.Collection(
                        e.Defaults.OrderBy(kvp => kvp.Key),
                        kvp => Assert.Equal(new KeyValuePair<string, object>("slug", "hello"), kvp));
                });
        }

        // These actions seem like duplicates, but this is a real case that can happen where two different
        // actions define the same route info. Link generation happens based on the action name + controller
        // name.
        [Fact]
        public void AttributeRoute_GetEntries_CreatesInboundEntry_CombinesLikeActions()
        {
            // Arrange
            var actions = new List<ActionDescriptor>()
            {
                new ActionDescriptor()
                {
                    AttributeRouteInfo = new AttributeRouteInfo()
                    {
                        Template = "api/Blog/{id}",
                        Name = "BLOG_INDEX",
                        Order = 17,
                    },
                    RouteValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "controller", "Blog" },
                        { "action", "Index" },
                    },
                },
                new ActionDescriptor()
                {
                    AttributeRouteInfo = new AttributeRouteInfo()
                    {
                        Template = "api/Blog/{id}",
                        Name = "BLOG_INDEX",
                        Order = 17,
                    },
                    RouteValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "controller", "Blog" },
                        { "action", "Index2" },
                    },
                },
            };

            var builder = CreateBuilder();
            var actionDescriptorProvider = CreateActionDescriptorProvider(actions);
            var route = CreateRoute(CreateHandler().Object, actionDescriptorProvider.Object);

            // Act
            route.AddEntries(builder, actionDescriptorProvider.Object.ActionDescriptors);

            // Assert
            Assert.Collection(
                builder.InboundEntries,
                e =>
                {
                    Assert.Empty(e.Constraints);
                    Assert.Equal(17, e.Order);
                    Assert.Equal(RoutePrecedence.ComputeInbound(e.RouteTemplate), e.Precedence);
                    Assert.Equal("BLOG_INDEX", e.RouteName);
                    Assert.Equal("api/Blog/{id}", e.RouteTemplate.TemplateText);
                    Assert.Empty(e.Defaults);
                });
        }

        private static TreeRouteBuilder CreateBuilder()
        {
            var services = new ServiceCollection()
                .AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>()
                .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
                .AddLogging()
                .AddRouting()
                .AddOptions()
                .BuildServiceProvider();
            return services.GetRequiredService<TreeRouteBuilder>();
        }

        private static Mock<IRouter> CreateHandler()
        {
            var handler = new Mock<IRouter>(MockBehavior.Strict);
            handler
                .Setup(h => h.RouteAsync(It.IsAny<RouteContext>()))
                .Callback<RouteContext>(c => c.Handler = NullHandler)
                .Returns(TaskCache.CompletedTask)
                .Verifiable();
            return handler;
        }

        private static Mock<IActionDescriptorCollectionProvider> CreateActionDescriptorProvider(
            IReadOnlyList<ActionDescriptor> actions)
        {
            var actionDescriptorProvider = new Mock<IActionDescriptorCollectionProvider>(MockBehavior.Strict);
            actionDescriptorProvider
                .SetupGet(ad => ad.ActionDescriptors)
                .Returns(new ActionDescriptorCollection(actions, version: 1));

            return actionDescriptorProvider;
        }

        private static AttributeRoute CreateRoute(
            IRouter handler, 
            IActionDescriptorCollectionProvider actionDescriptorProvider)
        {
            return CreateRoute((_) => handler, actionDescriptorProvider);
        }

        private static AttributeRoute CreateRoute(
            Func<ActionDescriptor[], IRouter> handlerFactory,
            IActionDescriptorCollectionProvider actionDescriptorProvider)
        {
            var services = new ServiceCollection()
                .AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>()
                .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
                .AddLogging()
                .AddRouting()
                .AddOptions()
                .BuildServiceProvider();
            return new AttributeRoute(actionDescriptorProvider, services, handlerFactory);
        }

        // Needed because new RouteValueDictionary(values) would give us all the properties of
        // the Dictionary class.
        private static RouteValueDictionary ToRouteValueDictionary(IDictionary<string, string> values)
        {
            var result = new RouteValueDictionary();
            foreach (var kvp in values)
            {
                result.Add(kvp.Key, kvp.Value);
            }

            return result;
        }
    }
}