using Data8.PowerPlatform.Dataverse.Client.ADAuthHelpers;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
#if NET7_0_OR_GREATER
using System.Buffers;
using System.Net.Security;
#else
using NSspi.Contexts;
using NSspi.Credentials;
#endif
using System;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace Data8.PowerPlatform.Dataverse.Client
{
    /// <summary>
    /// Inner client to set up the SOAP channel using WS-Trust with SSPI auth
    /// </summary>
    class ADAuthClient : IOrganizationServiceAsync2, IInnerOrganizationService
    {
        private readonly string _url;
        private readonly string _domain;
        private readonly string _username;
        private readonly string _password;
        private readonly string _upn;
        private readonly ProxySerializationSurrogate _serializationSurrogate;
        private DateTime _tokenExpires;
        private byte[] _proofToken;
        private SecurityContextToken _securityContextToken;

        /// <summary>
        /// Creates a new <see cref="ADAuthClient"/>
        /// </summary>
        /// <param name="url">The URL of the organization service</param>
        /// <param name="username">The username to authenticate as</param>
        /// <param name="password">The password to authenticate as</param>
        /// <param name="upn">The UPN the server process is running under</param>
        public ADAuthClient(string url, string username, string password, string upn)
        {
#if !NET7_0_OR_GREATER
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                throw new PlatformNotSupportedException("Windows authentication is only available on Windows clients or when using .NET 7");
            }
#endif

            _url = url;
            _upn = upn;
            _serializationSurrogate = new ProxySerializationSurrogate();
            Timeout = TimeSpan.FromSeconds(30);

            if (!string.IsNullOrEmpty(username))
            {
                // Split username into domain + username
                var domain = "";
                var parts = username.Split('\\');

                if (parts.Length == 2)
                {
                    domain = parts[0];
                    username = parts[1];
                }
                else if (parts.Length == 1)
                {
                    parts = username.Split('@');

                    if (parts.Length == 2)
                    {
                        domain = parts[1];
                        username = parts[0];
                    }
                }

                _domain = domain;
                _username = username;
                _password = password;
            }
        }

        /// <summary>
        /// Returns or sets the timeout for executing requests
        /// </summary>
        public TimeSpan Timeout { get; set; }

        /// <summary>
        /// Returns or sets the SDK version that will be reported to the server
        /// </summary>
        public string SdkClientVersion { get; set; }

        /// <summary>
        /// Returns or sets the impersonated user ID
        /// </summary>
        public Guid CallerId { get; set; }

        /// <summary>
        /// Authenticates with the server
        /// </summary>
        private void Authenticate()
        {
            if (_tokenExpires > DateTime.UtcNow.AddSeconds(10))
            {
                return;
            }

#if NET7_0_OR_GREATER

            var cred = string.IsNullOrEmpty(_username) ? CredentialCache.DefaultNetworkCredentials : new NetworkCredential(_username, _password, _domain);

            var context = new NegotiateAuthentication(new NegotiateAuthenticationClientOptions
            {
                AllowedImpersonationLevel = System.Security.Principal.TokenImpersonationLevel.Identification,
                Credential = cred,
                RequiredProtectionLevel = ProtectionLevel.EncryptAndSign,
                TargetName = _upn
            });
            var token = context.GetOutgoingBlob(Array.Empty<byte>(), out var state);

            if (state != NegotiateAuthenticationStatusCode.ContinueNeeded)
            {
                if (state == NegotiateAuthenticationStatusCode.Unsupported && Environment.OSVersion.Platform == PlatformID.Unix)
                {
                    throw new ApplicationException("Error authenticating with the server: " + state + ". Ensure you have the gss-ntlmssp package installed.");
                }

                throw new ApplicationException("Error authenticating with the server: " + state);
            }
#else
            // Set up the SSPI context

            var cred = string.IsNullOrEmpty(_username) ? (Credential)new CurrentCredential(NSspi.PackageNames.Negotiate, CredentialUse.Outbound) : new PasswordCredential(_domain, _username, _password, NSspi.PackageNames.Negotiate, CredentialUse.Outbound);

            var context = new ClientContext(cred, _upn, ContextAttrib.ReplayDetect | ContextAttrib.SequenceDetect | ContextAttrib.Confidentiality | ContextAttrib.InitIdentify);
            var state = context.Init(null, out var token);

            if (state != NSspi.SecurityStatus.ContinueNeeded)
            {
                throw new ApplicationException("Error authenticating with the server: " + state);
            }
#endif

            // Keep a hash of all the RSTs and RSTRs that have been sent so we can validate the authenticator
            // at the end.
            var auth = new Authenticator();

            var rst = new RequestSecurityToken(token);
            var resp = rst.Execute(_url, auth);

            var finalResponse = resp as RequestSecurityTokenResponseCollection;

            // Keep exchanging tokens until we get a full RSTR
            while (finalResponse == null)
            {
                if (!(resp is RequestSecurityTokenResponse r)) continue;

#if NET7_0_OR_GREATER
                    token = context.GetOutgoingBlob(r.BinaryExchange.Token, out state);

                    if (state != NegotiateAuthenticationStatusCode.Completed && state != NegotiateAuthenticationStatusCode.ContinueNeeded)
                    {
                        throw new ApplicationException("Error authenticating with the server: " + state);
                    }
#else
                state = context.Init(r.BinaryExchange.Token, out token);

                if (state != NSspi.SecurityStatus.OK && state != NSspi.SecurityStatus.ContinueNeeded)
                {
                    throw new ApplicationException("Error authenticating with the server: " + state);
                }
#endif

                resp = new RequestSecurityTokenResponse(r.Context, token).Execute(_url, auth);
                finalResponse = resp as RequestSecurityTokenResponseCollection;
            }

            var wrappedToken = finalResponse.Responses[0].RequestedProofToken.CipherValue;
            _tokenExpires = finalResponse.Responses[0].Lifetime.Expires;
            _securityContextToken = finalResponse.Responses[0].RequestedSecurityToken;

#if NET7_0_OR_GREATER
            if (state != NegotiateAuthenticationStatusCode.Completed)
            {
                token = context.GetOutgoingBlob(finalResponse.Responses[0].BinaryExchange.Token, out state);
            }

            if (state != NegotiateAuthenticationStatusCode.Completed)
            {
                throw new ApplicationException("Error authenticating with the server: " + state);
            }

            var unwrappedTokenWriter = new ArrayBufferWriter<byte>(wrappedToken.Length);
            state = context.Unwrap(wrappedToken, unwrappedTokenWriter, out _);

            if (state != NegotiateAuthenticationStatusCode.Completed)
            {
                throw new ApplicationException("Error authenticating with the server: " + state);
            }

            _proofToken = unwrappedTokenWriter.WrittenSpan.ToArray();
#else
            if (state != NSspi.SecurityStatus.OK)
            {
                state = context.Init(finalResponse.Responses[0].BinaryExchange.Token, out _);
            }

            if (state != NSspi.SecurityStatus.OK)
            {
                throw new ApplicationException("Error authenticating with the server: " + state);
            }

            _proofToken = context.Decrypt(wrappedToken, true);
#endif

            // Check the authenticator is valid
            auth.Validate(_proofToken, finalResponse.Responses[1].Authenticator.Token);
        }

        public void EnableProxyTypes(Assembly assembly)
        {
            _serializationSurrogate.LoadAssembly(assembly);
        }

        /// <inheritdoc/>
        public OrganizationResponse Execute(OrganizationRequest request)
        {
            Authenticate();

            var message = Message.CreateMessage(MessageVersion.Soap12WSAddressing10, "http://schemas.microsoft.com/xrm/2011/Contracts/Services/IOrganizationService/Execute", new ExecuteRequestWriter(request, _serializationSurrogate));
            message.Headers.MessageId = new UniqueId(Guid.NewGuid());
            message.Headers.ReplyTo = new System.ServiceModel.EndpointAddress("http://www.w3.org/2005/08/addressing/anonymous");
            message.Headers.To = new Uri(_url);
            message.Headers.Add(MessageHeader.CreateHeader("SdkClientVersion", Namespaces.Xrm2011Contracts, SdkClientVersion));
            message.Headers.Add(MessageHeader.CreateHeader("UserType", Namespaces.Xrm2011Contracts, "CrmUser"));
            message.Headers.Add(new SecurityHeader(_securityContextToken, _proofToken));

            if (CallerId != Guid.Empty)
                message.Headers.Add(MessageHeader.CreateHeader("CallerId", Namespaces.Xrm2011Contracts, CallerId));

            var req = WebRequest.CreateHttp(_url);
            req.Method = "POST";
            req.ContentType = "application/soap+xml; charset=utf-8";
            req.Timeout = (int)Timeout.TotalMilliseconds;

            using (var reqStream = req.GetRequestStream())
            using (var xmlTextWriter = XmlWriter.Create(reqStream, new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = false,
                Encoding = new UTF8Encoding(false),
                CloseOutput = true
            }))
            using (var xmlWriter = XmlDictionaryWriter.CreateDictionaryWriter(xmlTextWriter))
            {
                message.WriteMessage(xmlWriter);
                xmlWriter.WriteEndDocument();
                xmlWriter.Flush();
            }

            try
            {
                using (var resp = req.GetResponse())
                using (var respStream = resp.GetResponseStream())
                {
                    var reader = XmlReader.Create(respStream, new XmlReaderSettings());
                    var responseMessage = Message.CreateMessage(reader, 0x10000, MessageVersion.Soap12WSAddressing10);
                    var action = responseMessage.Headers.Action;

                    using (var bodyReader = responseMessage.GetReaderAtBodyContents())
                    {
                        bodyReader.ReadStartElement("ExecuteResponse", Namespaces.Xrm2011Services);

#if NETCOREAPP
                        var serializer = new DataContractSerializer(typeof(OrganizationResponse), "ExecuteResult", Namespaces.Xrm2011Services);
                        serializer.SetSerializationSurrogateProvider(_serializationSurrogate);
#else
                        var serializer = new DataContractSerializer(typeof(OrganizationResponse), "ExecuteResult", Namespaces.Xrm2011Services, null, int.MaxValue, false, true, _serializationSurrogate);
#endif
                        var response = (OrganizationResponse)serializer.ReadObject(bodyReader, true, new KnownTypesResolver());

                        bodyReader.ReadEndElement(); // ExecuteRepsonse

                        return response;
                    }
                }
            }
            catch (WebException ex)
            when (ex.Response != null)
            {
                using (var errorStream = ex.Response.GetResponseStream())
                {
                    var reader = XmlReader.Create(errorStream, new XmlReaderSettings());
                    var responseMessage = Message.CreateMessage(reader, 0x10000, MessageVersion.Soap12WSAddressing10);
                    var responseAction = responseMessage.Headers.Action;

                    using (var bodyReader = responseMessage.GetReaderAtBodyContents())
                    {
                        if (bodyReader.LocalName == "Fault" && bodyReader.NamespaceURI == Namespaces.Soap)
                            throw FaultReader.ReadFault(bodyReader, responseAction);

                        throw;
                    }
                }
            }
        }

        /// <inheritdoc/>
        public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            var request = new AssociateRequest
            {
                Target = new EntityReference(entityName, entityId),
                Relationship = relationship,
                RelatedEntities = relatedEntities
            };
            Execute(request);
        }

        /// <inheritdoc/>
        public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            var request = new DisassociateRequest
            {
                Target = new EntityReference(entityName, entityId),
                Relationship = relationship,
                RelatedEntities = relatedEntities
            };
            Execute(request);
        }

        /// <inheritdoc/>
        public Guid Create(Entity entity)
        {
            var request = new CreateRequest
            {
                Target = entity
            };
            var response = (CreateResponse)Execute(request);
            return response.id;
        }

        /// <inheritdoc/>
        public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
        {
            var request = new RetrieveRequest
            {
                Target = new EntityReference(entityName, id),
                ColumnSet = columnSet
            };
            var response = (RetrieveResponse)Execute(request);
            return response.Entity;
        }

        /// <inheritdoc/>
        public EntityCollection RetrieveMultiple(QueryBase query)
        {
            var request = new RetrieveMultipleRequest
            {
                Query = query
            };
            var response = (RetrieveMultipleResponse)Execute(request);
            return response.EntityCollection;
        }

        /// <inheritdoc/>
        public void Update(Entity entity)
        {
            var request = new UpdateRequest
            {
                Target = entity
            };
            Execute(request);
        }

        /// <inheritdoc/>
        public void Delete(string entityName, Guid id)
        {
            var request = new DeleteRequest
            {
                Target = new EntityReference(entityName, id)
            };
            Execute(request);
        }



        /// <inheritdoc/>
        public async Task<OrganizationResponse> ExecuteAsync(OrganizationRequest request)
        {
            return await ExecuteAsync(request, CancellationToken.None);
        }

        /// <inheritdoc/>
        public async Task<OrganizationResponse> ExecuteAsync(OrganizationRequest request, CancellationToken cancellationToken)
        {
            Authenticate();
            cancellationToken.ThrowIfCancellationRequested();

            var message = Message.CreateMessage(MessageVersion.Soap12WSAddressing10, "http://schemas.microsoft.com/xrm/2011/Contracts/Services/IOrganizationService/Execute", new ExecuteRequestWriter(request, _serializationSurrogate));
            message.Headers.MessageId = new UniqueId(Guid.NewGuid());
            message.Headers.ReplyTo = new System.ServiceModel.EndpointAddress("http://www.w3.org/2005/08/addressing/anonymous");
            message.Headers.To = new Uri(_url);
            message.Headers.Add(MessageHeader.CreateHeader("SdkClientVersion", Namespaces.Xrm2011Contracts, SdkClientVersion));
            message.Headers.Add(MessageHeader.CreateHeader("UserType", Namespaces.Xrm2011Contracts, "CrmUser"));
            message.Headers.Add(new SecurityHeader(_securityContextToken, _proofToken));

            if (CallerId != Guid.Empty)
                message.Headers.Add(MessageHeader.CreateHeader("CallerId", Namespaces.Xrm2011Contracts, CallerId));

            var req = WebRequest.CreateHttp(_url);
            req.Method = "POST";
            req.ContentType = "application/soap+xml; charset=utf-8";
            req.Timeout = (int)Timeout.TotalMilliseconds;

            using (var reqStream = await req.GetRequestStreamAsync().ConfigureAwait(false))
            using (var xmlTextWriter = XmlWriter.Create(reqStream, new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = false,
                Encoding = new UTF8Encoding(false),
                CloseOutput = true
            }))
            using (var xmlWriter = XmlDictionaryWriter.CreateDictionaryWriter(xmlTextWriter))
            {
                message.WriteMessage(xmlWriter);
                await xmlWriter.WriteEndDocumentAsync();
                await xmlWriter.FlushAsync();
            }
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using (var resp = await req.GetResponseAsync().ConfigureAwait(false))
                using (var respStream = resp.GetResponseStream())
                {
                    var reader = XmlReader.Create(respStream, new XmlReaderSettings());
                    var responseMessage = Message.CreateMessage(reader, 0x10000, MessageVersion.Soap12WSAddressing10);
                    var action = responseMessage.Headers.Action;

                    using (var bodyReader = responseMessage.GetReaderAtBodyContents())
                    {
                        bodyReader.ReadStartElement("ExecuteResponse", Namespaces.Xrm2011Services);

#if NETCOREAPP
                        var serializer = new DataContractSerializer(typeof(OrganizationResponse), "ExecuteResult", Namespaces.Xrm2011Services);
                        serializer.SetSerializationSurrogateProvider(_serializationSurrogate);
#else
                        var serializer = new DataContractSerializer(typeof(OrganizationResponse), "ExecuteResult", Namespaces.Xrm2011Services, null, int.MaxValue, false, true, _serializationSurrogate);
#endif
                        var response = (OrganizationResponse)serializer.ReadObject(bodyReader, true, new KnownTypesResolver());

                        bodyReader.ReadEndElement(); // ExecuteRepsonse

                        return response;
                    }
                }
            }
            catch (WebException ex)
            when (ex.Response != null)
            {
                using (var errorStream = ex.Response.GetResponseStream())
                {
                    var reader = XmlReader.Create(errorStream, new XmlReaderSettings());
                    var responseMessage = Message.CreateMessage(reader, 0x10000, MessageVersion.Soap12WSAddressing10);
                    var responseAction = responseMessage.Headers.Action;

                    using (var bodyReader = responseMessage.GetReaderAtBodyContents())
                    {
                        if (bodyReader.LocalName == "Fault" && bodyReader.NamespaceURI == Namespaces.Soap)
                            throw FaultReader.ReadFault(bodyReader, responseAction);

                        throw;
                    }
                }
            }
        }

        /// <inheritdoc/>
        public async Task AssociateAsync(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            await AssociateAsync(entityName, entityId, relationship, relatedEntities, CancellationToken.None);
        }

        /// <inheritdoc/>
        public async Task AssociateAsync(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities, CancellationToken cancellationToken)
        {
            var request = new AssociateRequest
            {
                Target = new EntityReference(entityName, entityId),
                Relationship = relationship,
                RelatedEntities = relatedEntities
            };
            await ExecuteAsync(request, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task DisassociateAsync(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            await DisassociateAsync(entityName, entityId, relationship, relatedEntities, CancellationToken.None);
        }

        /// <inheritdoc/>
        public async Task DisassociateAsync(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities, CancellationToken cancellationToken)
        {
            var request = new DisassociateRequest
            {
                Target = new EntityReference(entityName, entityId),
                Relationship = relationship,
                RelatedEntities = relatedEntities
            };
            await ExecuteAsync(request, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<Guid> CreateAsync(Entity entity)
        {
            return await CreateAsync(entity, CancellationToken.None).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<Guid> CreateAsync(Entity entity, CancellationToken cancellationToken)
        {
            var request = new CreateRequest { Target = entity };
            var response = (CreateResponse)await ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
            return response.id;
        }

        /// <inheritdoc/>
        public async Task<Entity> CreateAndReturnAsync(Entity entity, CancellationToken cancellationToken)
        {
            var id = await CreateAsync(entity, cancellationToken).ConfigureAwait(false);
            return await RetrieveAsync(entity.LogicalName, id, new ColumnSet(true), cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<Entity> RetrieveAsync(string entityName, Guid id, ColumnSet columnSet)
        {
            return await RetrieveAsync(entityName, id, columnSet, CancellationToken.None);
        }

        /// <inheritdoc/>
        public async Task<Entity> RetrieveAsync(string entityName, Guid id, ColumnSet columnSet, CancellationToken cancellationToken)
        {
            var request = new RetrieveRequest
            {
                Target = new EntityReference(entityName, id),
                ColumnSet = columnSet
            };
            var response = (RetrieveResponse)await ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
            return response.Entity;
        }

        /// <inheritdoc/>
        public async Task<EntityCollection> RetrieveMultipleAsync(QueryBase query)
        {
            return await RetrieveMultipleAsync(query, CancellationToken.None);
        }

        /// <inheritdoc/>
        public async Task<EntityCollection> RetrieveMultipleAsync(QueryBase query, CancellationToken cancellationToken)
        {
            var request = new RetrieveMultipleRequest
            {
                Query = query
            };
            var response = (RetrieveMultipleResponse)await ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
            return response.EntityCollection;
        }

        /// <inheritdoc/>
        public async Task UpdateAsync(Entity entity)
        {
            await UpdateAsync(entity, CancellationToken.None);
        }

        /// <inheritdoc/>
        public async Task UpdateAsync(Entity entity, CancellationToken cancellationToken)
        {
            var request = new UpdateRequest
            {
                Target = entity
            };
            await ExecuteAsync(request, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task DeleteAsync(string entityName, Guid id)
        {
            await DeleteAsync(entityName, id, CancellationToken.None);
        }

        /// <inheritdoc/>
        public async Task DeleteAsync(string entityName, Guid id, CancellationToken cancellationToken)
        {
            var request = new DeleteRequest { Target = new EntityReference(entityName, id) };
            await ExecuteAsync(request, cancellationToken);
        }

        private class ExecuteRequestWriter : BodyWriter
        {
            private readonly OrganizationRequest _request;
            private readonly ProxySerializationSurrogate _serializationSurrogate;

            public ExecuteRequestWriter(OrganizationRequest request, ProxySerializationSurrogate serializationSurrogate) : base(isBuffered: true)
            {
                _request = request;
                _serializationSurrogate = serializationSurrogate;
            }

            protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
            {
                writer.WriteStartElement("Execute", Namespaces.Xrm2011Services);

#if NETCOREAPP
                var serializer = new DataContractSerializer(typeof(OrganizationRequest), "request", Namespaces.Xrm2011Services);
                serializer.SetSerializationSurrogateProvider(_serializationSurrogate);
#else
                var serializer = new DataContractSerializer(typeof(OrganizationRequest), "request", Namespaces.Xrm2011Services, null, int.MaxValue, false, true, _serializationSurrogate);
#endif

                serializer.WriteObject(writer, _request, new KnownTypesResolver());

                writer.WriteEndElement(); // Execute
            }
        }
    }
}
