using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml.Serialization;

namespace Data8.PowerPlatform.Dataverse.Client.Wsdl
{
    /// <summary>
    /// Classes for parsing WSDL documents.
    /// System.ServiceModel.Description.ServiceEndpoint is not currently available in .NET Core - hopefully this will
    /// be added in the future - https://github.com/dotnet/wcf/issues/4464 - so this implements the specific parts
    /// we require.
    /// </summary>
    class Namespaces
    {
        public const string wsdl = "http://schemas.xmlsoap.org/wsdl/";
        public const string wsam = "http://www.w3.org/2007/05/addressing/metadata";
        public const string wsx = "http://schemas.xmlsoap.org/ws/2004/09/mex";
        public const string wsap = "http://schemas.xmlsoap.org/ws/2004/08/addressing/policy";
        public const string msc = "http://schemas.microsoft.com/ws/2005/12/wsdl/contract";
        public const string msxrm = "http://schemas.microsoft.com/xrm/2011/Contracts/Services";
        public const string wsp = "http://schemas.xmlsoap.org/ws/2004/09/policy";
        public const string xsd = "http://www.w3.org/2001/XMLSchema";
        public const string soap = "http://schemas.xmlsoap.org/wsdl/soap/";
        public const string wsu = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";
        public const string soap12 = "http://schemas.xmlsoap.org/wsdl/soap12/";
        public const string soapenc = "http://schemas.xmlsoap.org/soap/encoding/";
        public const string tns = "http://schemas.microsoft.com/xrm/2011/Contracts";
        public const string wsa10 = "http://www.w3.org/2005/08/addressing";
        public const string wsaw = "http://www.w3.org/2006/05/addressing/wsdl";
        public const string wsa = "http://schemas.xmlsoap.org/ws/2004/08/addressing";
        public const string sp = "http://docs.oasis-open.org/ws-sx/ws-securitypolicy/200702";
        public const string o = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
    }

    public static class WsdlLoader
    {
        public static IEnumerable<Definitions> Load(string url)
        {
            var loaded = new HashSet<string>();
            return Load(loaded, url);
        }

        private static IEnumerable<Definitions> Load(HashSet<string> loaded, string url)
        {
            if (!loaded.Add(url))
                yield break;

            var req = WebRequest.CreateHttp(url);
            using (var resp = req.GetResponse())
            using (var stream = resp.GetResponseStream())
            {
                var serializer = new XmlSerializer(typeof(Definitions));
                var wsdl = (Definitions)serializer.Deserialize(stream);

                yield return wsdl;

                if (wsdl.Imports != null)
                {
                    foreach (var import in wsdl.Imports)
                    {
                        foreach (var child in Load(loaded, import.Location))
                            yield return child;
                    }
                }
            }
        }
    }

    [XmlRoot("definitions", Namespace = Namespaces.wsdl)]
    public class Definitions
    {
        [XmlElement("import", Namespace = Namespaces.wsdl)]
        public Import[] Imports { get; set; }

        [XmlElement("Policy", Namespace = Namespaces.wsp)]
        public Policy[] Policies { get; set; }

        [XmlElement("binding", Namespace = Namespaces.wsdl)]
        public Binding[] Bindings { get; set; }

        [XmlElement("service", Namespace = Namespaces.wsdl)]
        public Service[] Services { get; set; }
    }

    public class Import
    {
        [XmlAttribute("location")]
        public string Location { get; set; }
    }

    public class PolicyItem
    {
    }

    public class MultiPolicy : PolicyItem
    {
        [XmlElement("ExactlyOne", Namespace = Namespaces.wsp, Type = typeof(ExactlyOne))]
        [XmlElement("All", Namespace = Namespaces.wsp, Type = typeof(All))]
        [XmlElement("EndorsingSupportingTokens", Namespace = Namespaces.sp, Type = typeof(EndorsingSupportingTokens))]
        [XmlElement("IssuedToken", Namespace = Namespaces.sp, Type = typeof(IssuedToken))]
        [XmlElement("SignedEncryptedSupportingTokens", Namespace = Namespaces.sp, Type = typeof(SignedEncryptedSupportingTokens))]
        [XmlElement("UsernameToken", Namespace = Namespaces.sp, Type = typeof(UsernameToken))]
        [XmlElement("Trust13", Namespace = Namespaces.sp, Type = typeof(Trust13))]
        [XmlElement("AuthenticationPolicy", Namespace = Namespaces.msxrm, Type = typeof(AuthenticationPolicy))]
        public PolicyItem[] PolicyItems { get; set; }

        public T FindPolicyItem<T>() where T : PolicyItem
        {
            if (PolicyItems == null)
                return null;

            var match = PolicyItems.OfType<T>().FirstOrDefault();

            if (match != null)
                return match;

            return PolicyItems
                .OfType<MultiPolicy>()
                .Select(child => child.FindPolicyItem<T>())
                .Where(m => m != null)
                .FirstOrDefault();
        }
    }

    public class Policy : MultiPolicy
    {
        [XmlAttribute("Id", Namespace = Namespaces.wsu)]
        public string Id { get; set; }
    }

    public class ExactlyOne : MultiPolicy
    {
    }

    public class All : MultiPolicy
    {
    }

    public class EndorsingSupportingTokens : PolicyItem
    {
        [XmlElement("Policy", Namespace = Namespaces.wsp)]
        public Policy Policy { get; set; }
    }

    public class IssuedToken : PolicyItem
    {
        public Issuer Issuer { get; set; }
    }

    public class Issuer
    {
        [XmlElement("Metadata", Namespace = Namespaces.wsa10)]
        public AddressMetadata Metadata { get; set; }
    }

    public class AddressMetadata
    {
        [XmlElement("Metadata", Namespace = Namespaces.wsx)]
        public SoapMetadata Metadata { get; set; }
    }

    public class SoapMetadata
    {
        [XmlElement("MetadataSection", Namespace = Namespaces.wsx)]
        public MetadataSection MetadataSection { get; set; }
    }

    public class MetadataSection
    {
        [XmlElement("MetadataReference", Namespace = Namespaces.wsx)]
        public MetadataReference MetadataReference { get; set; }
    }

    public class MetadataReference
    {
        [XmlElement("Address", Namespace = Namespaces.wsa10)]
        public string Address { get; set; }
    }

    public class SignedEncryptedSupportingTokens : PolicyItem
    {
        [XmlElement("Policy", Namespace = Namespaces.wsp)]
        public Policy Policy { get; set; }
    }

    public class UsernameToken : PolicyItem
    {
    }

    public class Trust13 : PolicyItem
    {
    }

    public class AuthenticationPolicy : PolicyItem
    {
        [XmlElement("Authentication", Namespace = Namespaces.msxrm)]
        public AuthenticationType Authentication { get; set; }
    }

    public enum AuthenticationType
    {
        ActiveDirectory,
        Federation
    }

    public class Binding
    {
        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlElement("PolicyReference", Namespace = Namespaces.wsp)]
        public PolicyReference PolicyReference { get; set; }
    }

    public class PolicyReference
    {
        [XmlAttribute("URI")]
        public string Uri { get; set; }
    }

    public class Service
    {
        [XmlElement("port", Namespace = Namespaces.wsdl)]
        public Port[] Ports { get; set; }
    }

    public class Port
    {
        [XmlAttribute("binding")]
        public string Binding { get; set; }

        [XmlElement("address", Namespace = Namespaces.soap12)]
        public SoapAddress Address { get; set; }
    }

    public class SoapAddress
    {
        [XmlAttribute("location")]
        public string Location { get; set; }
    }
}
