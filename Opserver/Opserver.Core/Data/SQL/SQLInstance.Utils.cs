﻿using System;
using System.Threading.Tasks;

namespace StackExchange.Opserver.Data.SQL
{
    public partial class SQLInstance
    {
        /// <summary>
        /// Removes a query plan from the cache
        /// </summary>
        public async Task<int> RemovePlanAsync(byte[] planHandle)
        {
            try
            {
                using (var conn = await GetConnectionAsync().ConfigureAwait(false))
                {
                    return await conn.ExecuteAsync("DBCC FREEPROCCACHE (@planHandle);", new { planHandle }).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Current.LogException(ex);
                return 0;
            }
        }
    }
}
