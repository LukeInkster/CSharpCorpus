// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Utilities;

namespace Microsoft.EntityFrameworkCore.Infrastructure
{
    /// <summary>
    ///     <para>
    ///         Base class for types that support reading and writing annotations.
    ///     </para>
    ///     <para>
    ///         This type is typically used by database providers (and other extensions). It is generally
    ///         not used in application code.
    ///     </para>
    /// </summary>
    public class Annotatable : IMutableAnnotatable
    {
        private readonly LazyRef<SortedDictionary<string, Annotation>> _annotations =
            new LazyRef<SortedDictionary<string, Annotation>>(() => new SortedDictionary<string, Annotation>());

        /// <summary>
        ///     Gets all annotations on the current object.
        /// </summary>
        public virtual IEnumerable<Annotation> GetAnnotations() =>
            _annotations.HasValue
                ? _annotations.Value.Values
                : Enumerable.Empty<Annotation>();

        /// <summary>
        ///     Adds an annotation to this object. Throws if an annotation with the specified name already exists.
        /// </summary>
        /// <param name="name"> The key of the annotation to be added. </param>
        /// <param name="value"> The value to be stored in the annotation. </param>
        /// <returns> The newly added annotation. </returns>
        public virtual Annotation AddAnnotation(string name, object value)
        {
            Check.NotEmpty(name, nameof(name));
            Check.NotNull(value, nameof(value));

            var annotation = CreateAnnotation(name, value);

            return AddAnnotation(name, annotation);
        }

        /// <summary>
        ///     Adds an annotation to this object. Throws if an annotation with the specified name already exists.
        /// </summary>
        /// <param name="name"> The key of the annotation to be added. </param>
        /// <param name="annotation"> The annotation to be added. </param>
        /// <returns> The added annotation. </returns>
        protected virtual Annotation AddAnnotation([NotNull] string name, [NotNull] Annotation annotation)
        {
            var previousLength = _annotations.Value.Count;
            SetAnnotation(name, annotation);

            if (previousLength == _annotations.Value.Count)
            {
                throw new InvalidOperationException(CoreStrings.DuplicateAnnotation(name));
            }

            return annotation;
        }

        /// <summary>
        ///     Sets the annotation stored under the given key. Overwrites the existing annotation if an 
        ///     annotation with the specified name already exists. 
        /// </summary>
        /// <param name="name"> The key of the annotation to be added. </param>
        /// <param name="annotation"> The annotation to be set. </param>
        /// <returns> The annotation that was set. </returns>
        protected virtual Annotation SetAnnotation([NotNull] string name, [NotNull] Annotation annotation)
        {
            _annotations.Value[name] = annotation;

            return annotation;
        }

        /// <summary>
        ///     Adds an annotation to this object or returns the existing annotation if one with the specified name
        ///     already exists.
        /// </summary>
        /// <param name="name"> The key of the annotation to be added. </param>
        /// <param name="value"> The value to be stored in the annotation. </param>
        /// <returns>
        ///     The existing annotation if an annotation with the specified name already exists. Otherwise, the newly
        ///     added annotation.
        /// </returns>
        public virtual Annotation GetOrAddAnnotation([NotNull] string name, [NotNull] object value)
            => FindAnnotation(name) ?? AddAnnotation(name, value);

        /// <summary>
        ///     Gets the annotation with the given name, returning null if it does not exist.
        /// </summary>
        /// <param name="name"> The key of the annotation to find. </param>
        /// <returns>
        ///     The existing annotation if an annotation with the specified name already exists. Otherwise, null.
        /// </returns>
        public virtual Annotation FindAnnotation(string name)
        {
            Check.NotEmpty(name, nameof(name));

            if (!_annotations.HasValue)
            {
                return null;
            }

            Annotation annotation;
            return _annotations.Value.TryGetValue(name, out annotation)
                ? annotation
                : null;
        }

        /// <summary>
        ///     Removes the given annotation from this object.
        /// </summary>
        /// <param name="name"> The annotation to remove. </param>
        /// <returns> The annotation that was removed. </returns>
        public virtual Annotation RemoveAnnotation(string name)
        {
            Check.NotNull(name, nameof(name));

            var annotation = FindAnnotation(name);
            if (annotation == null)
            {
                return null;
            }

            _annotations.Value.Remove(name);

            return annotation;
        }

        /// <summary>
        ///     Gets the value annotation with the given name, returning null if it does not exist.
        /// </summary>
        /// <param name="name"> The key of the annotation to find. </param>
        /// <returns>
        ///     The value of the existing annotation if an annotation with the specified name already exists.
        ///     Otherwise, null.
        /// </returns>
        // ReSharper disable once AnnotationRedundancyInHierarchy
        // TODO: Fix API test to handle indexer
        public virtual object this[[NotNull] string name]
        {
            get { return FindAnnotation(name)?.Value; }
            [param: CanBeNull]
            set
            {
                Check.NotEmpty(name, nameof(name));

                if (value == null)
                {
                    RemoveAnnotation(name);
                }
                else
                {
                    _annotations.Value[name] = CreateAnnotation(name, value);
                }
            }
        }

        /// <summary>
        ///     Creates a new annotation.
        /// </summary>
        /// <param name="name"> The key of the annotation. </param>
        /// <param name="value"> The value to be stored in the annotation. </param>
        /// <returns> The newly created annotation. </returns>
        protected virtual Annotation CreateAnnotation([NotNull] string name, [NotNull] object value)
            => new Annotation(name, value);

        /// <summary>
        ///     Gets all annotations on the current object.
        /// </summary>
        IEnumerable<IAnnotation> IAnnotatable.GetAnnotations() => GetAnnotations();

        /// <summary>
        ///     Gets the annotation with the given name, returning null if it does not exist.
        /// </summary>
        /// <param name="name"> The key of the annotation to find. </param>
        /// <returns>
        ///     The existing annotation if an annotation with the specified name already exists. Otherwise, null.
        /// </returns>
        IAnnotation IAnnotatable.FindAnnotation(string name) => FindAnnotation(name);
    }
}
