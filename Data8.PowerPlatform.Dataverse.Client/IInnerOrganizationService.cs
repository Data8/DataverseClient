using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;

namespace Data8.PowerPlatform.Dataverse.Client
{
    internal interface IInnerOrganizationService : IOrganizationService
    {
        void EnableProxyTypes(Assembly assembly);

        TimeSpan Timeout { get; set; }
    }
}
