using System;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;

namespace Data8.PowerPlatform.Dataverse.Client
{
    static class TimeoutHelper
    {
        public static TimeSpan GetTimeout(this IOrganizationService svc)
        {
            if (svc is IContextChannel channel)
                return channel.OperationTimeout;

            if (svc is ADAuthClient ad)
                return ad.Timeout;

            if (svc is OrgServiceAsyncWrapper async)
                return async.Timeout;

            throw new NotSupportedException();
        }

        public static void SetTimeout(this IOrganizationService svc, TimeSpan timeout)
        {
            if (svc is IContextChannel channel)
            {
                channel.OperationTimeout = timeout;
                return;
            }

            if (svc is ADAuthClient ad)
            {
                ad.Timeout = timeout;
                return;
            }

            if (svc is OrgServiceAsyncWrapper async)
            {
                async.Timeout = timeout;
                return;
            }

            throw new NotSupportedException();
        }
    }
}
