// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xunit;

namespace Microsoft.AspNetCore.Mvc.IntegrationTests
{
    public class ServicesModelBinderIntegrationTest
    {
        [Fact]
        public async Task BindParameterFromService_WithData_GetsBound()
        {
            // Arrange
            var argumentBinder = ModelBindingTestHelper.GetArgumentBinder();
            var parameter = new ParameterDescriptor()
            {
                Name = "Parameter1",
                BindingInfo = new BindingInfo()
                {
                    BinderModelName = "CustomParameter",
                    BindingSource = BindingSource.Services
                },

                // Using a service type already in defaults.
                ParameterType = typeof(JsonOutputFormatter)
            };

            var testContext = ModelBindingTestHelper.GetTestContext();
            var modelState = testContext.ModelState;

            // Act
            var modelBindingResult = await argumentBinder.BindModelAsync(parameter, testContext);

            // Assert

            // ModelBindingResult
            Assert.True(modelBindingResult.IsModelSet);

            // Model
            var outputFormatter = Assert.IsType<JsonOutputFormatter>(modelBindingResult.Model);
            Assert.NotNull(outputFormatter);

            // ModelState
            Assert.True(modelState.IsValid);
            Assert.Empty(modelState.Keys);
        }

        [Fact]
        public async Task BindParameterFromService_NoPrefix_GetsBound()
        {
            // Arrange
            var argumentBinder = ModelBindingTestHelper.GetArgumentBinder();
            var parameter = new ParameterDescriptor
            {
                Name = "ControllerProperty",
                BindingInfo = new BindingInfo
                {
                    BindingSource = BindingSource.Services,
                },

                // Use a service type already in defaults.
                ParameterType = typeof(JsonOutputFormatter),
            };

            var testContext = ModelBindingTestHelper.GetTestContext();
            var modelState = testContext.ModelState;

            // Act
            var modelBindingResult = await argumentBinder.BindModelAsync(parameter, testContext);

            // Assert
            // ModelBindingResult
            Assert.True(modelBindingResult.IsModelSet);

            // Model
            var outputFormatter = Assert.IsType<JsonOutputFormatter>(modelBindingResult.Model);
            Assert.NotNull(outputFormatter);

            // ModelState
            Assert.True(modelState.IsValid);
            Assert.Empty(modelState);
        }

        [Fact]
        public async Task BindEnumerableParameterFromService_NoPrefix_GetsBound()
        {
            // Arrange
            var argumentBinder = ModelBindingTestHelper.GetArgumentBinder();
            var parameter = new ParameterDescriptor
            {
                Name = "ControllerProperty",
                BindingInfo = new BindingInfo
                {
                    BindingSource = BindingSource.Services,
                },

                // Use a service type already in defaults.
                ParameterType = typeof(IEnumerable<JsonOutputFormatter>),
            };

            var testContext = ModelBindingTestHelper.GetTestContext();
            var modelState = testContext.ModelState;

            // Act
            var modelBindingResult = await argumentBinder.BindModelAsync(parameter, testContext);

            // Assert
            // ModelBindingResult
            Assert.True(modelBindingResult.IsModelSet);

            // Model
            var formatterArray = Assert.IsType<JsonOutputFormatter[]>(modelBindingResult.Model);
            Assert.Equal(1, formatterArray.Length);

            // ModelState
            Assert.True(modelState.IsValid);
            Assert.Empty(modelState);
        }

        [Fact]
        public async Task BindEnumerableParameterFromService_NoService_GetsBound()
        {
            // Arrange
            var argumentBinder = ModelBindingTestHelper.GetArgumentBinder();
            var parameter = new ParameterDescriptor
            {
                Name = "ControllerProperty",
                BindingInfo = new BindingInfo
                {
                    BindingSource = BindingSource.Services,
                },

                // Use a service type not available in DI.
                ParameterType = typeof(IEnumerable<IActionResult>),
            };

            var testContext = ModelBindingTestHelper.GetTestContext();
            var modelState = testContext.ModelState;

            // Act
            var modelBindingResult = await argumentBinder.BindModelAsync(parameter, testContext);

            // Assert
            // ModelBindingResult
            Assert.True(modelBindingResult.IsModelSet);

            // Model
            var actionResultArray = Assert.IsType<IActionResult[]>(modelBindingResult.Model);
            Assert.Equal(0, actionResultArray.Length);

            // ModelState
            Assert.True(modelState.IsValid);
            Assert.Empty(modelState);
        }

        [Fact]
        public async Task BindParameterFromService_NoService_Throws()
        {
            // Arrange
            var argumentBinder = ModelBindingTestHelper.GetArgumentBinder();
            var parameter = new ParameterDescriptor
            {
                Name = "ControllerProperty",
                BindingInfo = new BindingInfo
                {
                    BindingSource = BindingSource.Services,
                },

                // Use a service type not available in DI.
                ParameterType = typeof(IActionResult),
            };

            var testContext = ModelBindingTestHelper.GetTestContext();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => argumentBinder.BindModelAsync(parameter, testContext));
            Assert.Contains(typeof(IActionResult).FullName, exception.Message);
        }

        private class Person
        {
            public JsonOutputFormatter Service { get; set; }
        }

        // [FromServices] cannot be associated with a type. But a [FromServices] or [ModelBinder] subclass or custom
        // IBindingSourceMetadata implementation might not have the same restriction. Make sure the metadata is honored
        // when such an attribute is associated with a type somewhere in the type hierarchy of an action parameter.
        [Theory]
        [MemberData(
            nameof(BinderTypeBasedModelBinderIntegrationTest.NullAndEmptyBindingInfo),
            MemberType = typeof(BinderTypeBasedModelBinderIntegrationTest))]
        public async Task FromServicesOnPropertyType_WithData_Succeeds(BindingInfo bindingInfo)
        {
            // Arrange
            // Similar to a custom IBindingSourceMetadata implementation or [ModelBinder] subclass on a custom service.
            var metadataProvider = new TestModelMetadataProvider();
            metadataProvider
                .ForProperty<Person>(nameof(Person.Service))
                .BindingDetails(binding => binding.BindingSource = BindingSource.Services);

            var argumentBinder = ModelBindingTestHelper.GetArgumentBinder(metadataProvider);
            var parameter = new ParameterDescriptor
            {
                Name = "parameter-name",
                BindingInfo = bindingInfo,
                ParameterType = typeof(Person),
            };

            var testContext = ModelBindingTestHelper.GetTestContext();
            testContext.MetadataProvider = metadataProvider;
            var modelState = testContext.ModelState;

            // Act
            var modelBindingResult = await argumentBinder.BindModelAsync(parameter, testContext);

            // Assert
            Assert.True(modelBindingResult.IsModelSet);
            var person = Assert.IsType<Person>(modelBindingResult.Model);
            Assert.NotNull(person.Service);

            Assert.True(modelState.IsValid);
            Assert.Empty(modelState);
        }

        // [FromServices] cannot be associated with a type. But a [FromServices] or [ModelBinder] subclass or custom
        // IBindingSourceMetadata implementation might not have the same restriction. Make sure the metadata is honored
        // when such an attribute is associated with an action parameter's type.
        [Theory]
        [MemberData(
            nameof(BinderTypeBasedModelBinderIntegrationTest.NullAndEmptyBindingInfo),
            MemberType = typeof(BinderTypeBasedModelBinderIntegrationTest))]
        public async Task FromServicesOnParameterType_WithData_Succeeds(BindingInfo bindingInfo)
        {
            // Arrange
            // Similar to a custom IBindingSourceMetadata implementation or [ModelBinder] subclass on a custom service.
            var metadataProvider = new TestModelMetadataProvider();
            metadataProvider
                .ForType<JsonOutputFormatter>()
                .BindingDetails(binding => binding.BindingSource = BindingSource.Services);

            var argumentBinder = ModelBindingTestHelper.GetArgumentBinder(metadataProvider);
            var parameter = new ParameterDescriptor
            {
                Name = "parameter-name",
                BindingInfo = bindingInfo,
                ParameterType = typeof(JsonOutputFormatter),
            };

            var testContext = ModelBindingTestHelper.GetTestContext();
            testContext.MetadataProvider = metadataProvider;
            var modelState = testContext.ModelState;

            // Act
            var modelBindingResult = await argumentBinder.BindModelAsync(parameter, testContext);

            // Assert
            Assert.True(modelBindingResult.IsModelSet);
            Assert.IsType<JsonOutputFormatter>(modelBindingResult.Model);

            Assert.True(modelState.IsValid);
            Assert.Empty(modelState);
        }
    }
}