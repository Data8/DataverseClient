using System;
using System.Reflection;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace Data8.PowerPlatform.Dataverse.Client
{
    internal interface IInnerOrganizationService : IOrganizationServiceAsync2
    {
        void EnableProxyTypes(Assembly assembly);

        TimeSpan Timeout { get; set; }
    }
}
