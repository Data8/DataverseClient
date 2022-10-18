# Data8 .NET Core Client SDK for On-Premise Dynamics 365/CRM

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

var onPremIfd = new OnPremiseClient("https://org.crm.contoso.com/XRMServices/2011/Organization.svc", "AD\\username", "password!");
var onPremAD = new OnPremiseClient("https://crm.contoso.com/org/XRMServices/2011/Organization.svc", "AD\\username", "password!");
var online = new ServiceClient("AuthType=ClientSecret;Url=https://contoso.crm.dynamics.com;ClientId=637C79F7-AE71-4E9A-BD5B-1EC5EC9F397A;ClientSecret=p1UiydoIWwUH5AdMbiVBOrEYn8t4RXud");

CreateRecord(onPremIfd);
CreateRecord(onPremAD);
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

This package is designed to be used with on-premise Dynamics 365 instances. Supported authentication types are:

* Integrated Windows Authentication
* Claims-Based Authentication
* Internet Facing Deployment

The package targets .NET Core 3.1 or later. It can also be used on .NET Framework 4.6.2 or later.

## Notes on Integrated Windows Authentication

If claims-based authentication is not configured on your Dynamics 365 instance, you will be using Integrated Windows
Authentication. This library can authenticate to these instances, but only where the client is either:

* running on Windows, or
* running on Linux under .NET 7 and with the `gss-ntlmssp` package installed

You can choose to supply a username (in the format `DOMAIN\Username` or `username@domain`) and password, or leave
both blank to authenticate as the currently logged on user.

## Early bound support

This library supports both late-bound and early-bound code. To use early-bound classes, create them using
[CrmSvcUtil](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/org-service/generate-early-bound-classes)
or the [Early Bound Generator](https://www.xrmtoolbox.com/plugins/DLaB.Xrm.EarlyBoundGenerator/) tool in XrmToolBox
as normal. You'll then need to enable them using the `EnableProxyTypes` method in the same way as the CrmServiceClient uses.
After they are enabled you can pass your early bound instances and get strongly types responses from all the normal
`IOrganizationService` methods, or use the generated OrganizationServiceContext to interact with the service:

```csharp
var client = new OnPremiseClient(url, username, password);
client.EnableProxyTypes();

// Using IOrganizationService
var contact = new Contact { FirstName = "Early Bound", LastName = "Context" };
contact.Id = client.Create(contact);

// Using OrganizationServiceContext
var context = new CrmServiceContext(client);
var newContacts = context.ContactSet
    .Where(c => c.CreatedOn > DateTime.Today)
    .Select(c => new { c.FirstName, c.LastName })
    .ToList();
```

## Async support

As well as the standard `IOrganizationService` interface, this library also supports both the `IOrganizationServiceAsync`
and `IOrganizationServiceAsync2` interfaces to allow you to use async programming styles when accessing Dynamics 365 data.

The `IOrganizationServiceAsync2` interface supports cancellation. Similar to the Microsoft library, cancellation can only
be performed up to the point the request is sent to the server. Requests cannot be cancelled once they are sent.

## Thanks

Many thanks to [Data8](https://www.data-8.co.uk/) for the time to develop this library and release it for public use.

This project builds on the work of the [NSspi](https://github.com/antiduh/nsspi) library to handle the internals of
working with the Windows authentication functions pre-.NET 7. Unfortunately the latest release of NSspi was missing
a few required methods, so it is currently including some code from a fork of that library.

## Support and Contributing

This package is not officially supported, either by Data8 or Microsoft. We will attempt to provide support on a
best-efforts basis via GitHub issues but can't guarantee we will be able to resolve any specific issues you may have.

Contributions to the package are welcome in the form of suggestions via Issues or bug fixes/enhancements via
Pull Requests.
