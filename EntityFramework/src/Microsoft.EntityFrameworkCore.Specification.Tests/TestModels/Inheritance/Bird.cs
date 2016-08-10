// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.EntityFrameworkCore.Specification.Tests.TestModels.Inheritance
{
    public abstract class Bird : Animal
    {
        public bool IsFlightless { get; set; }
        public string EagleId { get; set; }
    }
}
