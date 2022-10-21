using System;
using System.Threading.Tasks;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Data8.PowerPlatform.Dataverse.Client
{
    internal class OrgServiceAsyncWrapper : IOrganizationServiceAsync
    {
        private readonly IOrganizationService _service;

        public OrgServiceAsyncWrapper(IOrganizationService service)
        {
            _service = service;
        }

        public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            _service.Associate(entityName, entityId, relationship, relatedEntities);
        }

        public Task AssociateAsync(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            return Task.Run(() => Associate(entityName, entityId, relationship, relatedEntities));
        }

        public Guid Create(Entity entity)
        {
            return _service.Create(entity);
        }

        public Task<Guid> CreateAsync(Entity entity)
        {
            return Task.Run(() => Create(entity));
        }

        public void Delete(string entityName, Guid id)
        {
            _service.Delete(entityName, id);
        }

        public Task DeleteAsync(string entityName, Guid id)
        {
            return Task.Run(() => Delete(entityName, id));
        }

        public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            _service.Disassociate(entityName, entityId, relationship, relatedEntities);
        }

        public Task DisassociateAsync(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            return Task.Run(() => _service.Disassociate(entityName, entityId, relationship, relatedEntities));
        }

        public OrganizationResponse Execute(OrganizationRequest request)
        {
            return _service.Execute(request);
        }

        public Task<OrganizationResponse> ExecuteAsync(OrganizationRequest request)
        {
            return Task.Run(() => _service.Execute(request));
        }

        public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
        {
            return _service.Retrieve(entityName, id, columnSet);
        }

        public Task<Entity> RetrieveAsync(string entityName, Guid id, ColumnSet columnSet)
        {
            return Task.Run(() => Retrieve(entityName, id, columnSet));
        }

        public EntityCollection RetrieveMultiple(QueryBase query)
        {
            return _service.RetrieveMultiple(query);
        }

        public Task<EntityCollection> RetrieveMultipleAsync(QueryBase query)
        {
            return Task.Run(() => RetrieveMultiple(query));
        }

        public void Update(Entity entity)
        {
            _service.Update(entity);
        }

        public Task UpdateAsync(Entity entity)
        {
            return Task.Run(() => Update(entity));
        }
    }
}
