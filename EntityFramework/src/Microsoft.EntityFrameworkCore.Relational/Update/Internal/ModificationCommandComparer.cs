// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.EntityFrameworkCore.Update.Internal
{
    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class ModificationCommandComparer : IComparer<ModificationCommand>
    {
        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public virtual int Compare(ModificationCommand x, ModificationCommand y)
        {
            var result = 0;
            if (ReferenceEquals(x, y))
            {
                return result;
            }

            if (x == null)
            {
                return -1;
            }

            if (y == null)
            {
                return 1;
            }

            result = StringComparer.Ordinal.Compare(x.Schema, y.Schema);
            if (0 != result)
            {
                return result;
            }

            result = StringComparer.Ordinal.Compare(x.TableName, y.TableName);
            if (0 != result)
            {
                return result;
            }

            result = (int)x.EntityState - (int)y.EntityState;
            if (0 != result)
            {
                return result;
            }

            return result;
        }
    }
}
