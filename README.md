# Data8 .NET Core Client SDK for On-Premise IFD Dynamics 365

The [Microsoft.PowerPlatform.Dataverse.Client](https://github.com/microsoft/PowerPlatform-DataverseServiceClient)
package provides an SDK for connecting to Dataverse & Dynamics 365 instances from .NET Core, but relies on OAuth
authentication. This poses a problem when you need to connect to an on-premise instance that either does not support
OAuth, or where the OAuth tokens regularly expire and cannot be automatically refreshed.

This package [Data8.PowerPlatform.Dataverse.Client](https://nuget.org/packages/Data8.PowerPlatform.Dataverse.Client)
builds on top of the Microsoft one and offers an alternative `IOrganizationService` implementation using WS-Trust.
This allows you to connect using the URL of the organization service, username and password without any additional
configuration.

Because this `OnPremiseClient` implements the same `IOrganizationService` as the standard `ServiceClient` implementation
your code can work with either as shown in the sample code below.

## Sample

```csharp
using Data8.PowerPlatform.Dataverse.Client;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;

var onPrem = new OnPremiseClient("https://org.crm.contoso.com/XRMServices/2011/Organization.svc", "AD\\username", "password!");
var online = new ServiceClient("AuthType=ClientSecret;Url=https://contoso.crm.dynamics.com;ClientId=637C79F7-AE71-4E9A-BD5B-1EC5EC9F397A;ClientSecret=p1UiydoIWwUH5AdMbiVBOrEYn8t4RXud");

CreateRecord(onPrem);
CreateRecord(online);

void CreateRecord(IOrganizationService svc)
{
	var entity = new Entity("account")
	{
		["name"] = "Data8"
	};

	entity.Id = svc.Create(entity);
}
```

## Compatibility

This package is designed to be used with on-premise Dynamics 365 instances configured with claims-based authentication.
An Internet Facing Deployment is also supported but not required.

Integrated Windows Authentication is not supported.

The package targets .NET Core 3.1 or later.

## Support and Contributing

This package is not officially supported, either by Data8 or Microsoft. We will attempt to provide support on a
best-efforts basis via GitHub issues but can't guarantee we will be able to resolve any specific issues you may have.

Contributions to the package are welcome in the form of suggestions via Issues or bug fixes/enhancements via
Pull Requests.
