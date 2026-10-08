using Spearing.Entities.ComplianceEntities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Spearing.Data.IComplianceData
{
    public interface IModelDefinitionData
    {
        ModelDefinition GetModelDefinition(int modelDefinitionID);
        Task<ModelDefinition> GetModelDefinitionAsync(int modelDefinitionID);

        IEnumerable<ModelDefinition> GetModelDefinitions();
        Task<IEnumerable<ModelDefinition>> GetModelDefinitionsAsync();

        void InsertModelDefinition(ModelDefinition modelDefinition);
        Task InsertModelDefinitionAsync(ModelDefinition modelDefinition);

        void UpdateModelDefinition(ModelDefinition modelDefinition);
        Task UpdateModelDefinitionAsync(ModelDefinition modelDefinition);
    }
}
