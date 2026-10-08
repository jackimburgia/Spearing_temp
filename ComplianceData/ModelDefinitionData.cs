using Spearing.Data.IComplianceData;
using Spearing.Entities.ComplianceEntities;
using System;
using System.Collections.Generic;
using System.Text;
using Spearing.Utilities.Data.DapperUtilities;
using Spearing.Utilities.Data.IDataUtilities;
using Spearing.Utilities.Data.SqlClientUtilities;
using static Spearing.Utilities.Data.SqlClientUtilities.ParameterUtilities;
using static Spearing.Utilities.Data.SqlClientUtilities.SqlConnectionExtensions;
using Dapper;
using System.Runtime.InteropServices;
using Microsoft.Data.SqlClient;

namespace Spearing.Data.ComplianceData
{

    public class ModelDefinitionData : IModelDefinitionData
    {
        protected IDbConnectionFactory dbConnectionFactory;

        public ModelDefinitionData(IDbConnectionFactory dbConnectionFactory)
        {
            this.dbConnectionFactory = dbConnectionFactory;
        }

        #region GetModelDefinition

        public ModelDefinition GetModelDefinition(int modelDefinitionID)
        {
            var results = this.GetModelDefinitionBuilder(modelDefinitionID)
                .Query((db, cmd) => db.QuerySingleOrDefault<ModelDefinition>(cmd));

            return results;
        }

        public async Task<ModelDefinition> GetModelDefinitionAsync(int modelDefinitionID)
        {
            var results = await this.GetModelDefinitionBuilder(modelDefinitionID)
                .Query((db, cmd) => db.QuerySingleOrDefaultAsync<ModelDefinition>(cmd));

            return results;
        }
        
        protected CommandDefinitionBuilder GetModelDefinitionBuilder(int modelDefinitionID)
        {
            var cmd = this.dbConnectionFactory
                .CreateConnection(DatabaseNames.Compliance)
                .CommandDefinition()
                .Text(@"
SELECT * 
FROM Configuration.ModelDefinition
WHERE ModelDefinitionID = @ModelDefinitionID
")
                .Params(
                    IntParam(nameof(ModelDefinition.ModelDefinitionID)).Value(modelDefinitionID)
                );

            return cmd;
        }

        #endregion


        #region GetModelDefinitions
        public IEnumerable<ModelDefinition> GetModelDefinitions()
        {
            var results = this.GetModelDefinitionsBuilder()
                .Query((db, cmd) => db.Query<ModelDefinition>(cmd));

            return results;
        }

        public async Task<IEnumerable<ModelDefinition>> GetModelDefinitionsAsync()
        {
            var results = await this.GetModelDefinitionsBuilder()
                .Query((db, cmd) => db.QueryAsync<ModelDefinition>(cmd));

            return results;
        }

        protected CommandDefinitionBuilder GetModelDefinitionsBuilder()
        {
            var cmd = this.dbConnectionFactory
                .CreateConnection(DatabaseNames.Compliance)
                .CommandDefinition()
                .Text("SELECT * FROM Configuration.ModelDefinition");

            return cmd;
        }

        #endregion


        #region InsertModelDefiniton
        public void InsertModelDefinition(ModelDefinition modelDefinition)
        {




        }

        public async Task InsertModelDefinitionAsync(ModelDefinition modelDefinition)
        {
            throw new NotImplementedException();
        }
        #endregion


        #region UpdateModelDefiniton
        public void UpdateModelDefinition(ModelDefinition modelDefinition)
        {
            var cmd = this.UpdateModelDefinitionCommand(modelDefinition);

            cmd.ExecNonQuery();
        }

        public async Task UpdateModelDefinitionAsync(ModelDefinition modelDefinition)
        {
            var cmd = this.UpdateModelDefinitionCommand(modelDefinition);

            await cmd.ExecNonQueryAsync();
        }

        protected SqlCommand UpdateModelDefinitionCommand(ModelDefinition modelDefinition)
        {
            var cmd = this.dbConnectionFactory
                .CreateConnection<SqlConnection>(DatabaseNames.Compliance)
                .Text(@"
UPDATE [Configuration].[ModelDefinition]
SET
    ModelDescr = @ModelDescr,
    CreatedBy = @CreatedBy,
    CreatedDate = @CreatedDate,
    ModifiedBy = @ModifiedBy,
    ModifiedDate = @ModifiedDate
WHERE
    ModelDefinitionID = @ModelDefinitionID
")
                .Params(
                    IntParam(nameof(ModelDefinition.ModelDefinitionID)).Value(modelDefinition.ModelDefinitionID),
                    VarCharParam(nameof(ModelDefinition.ModelDescr)).Value(modelDefinition.ModelDescr)
                )
                .Modifiable(modelDefinition);

            return cmd;
        }

        #endregion

    }
}
