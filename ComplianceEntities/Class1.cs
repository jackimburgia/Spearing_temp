using Spearing.Utilities.Entities.EntitiesUtilities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Spearing.Entities.ComplianceEntities
{
    public class ModelDefinition : IModifiable
    {
        public int ModelDefinitionID { get; set; }
        public string ModelDescr { get; set; }

        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime ModifiedDate { get; set; }
    }

    public enum ModelBuildTypes
    {
        NA,
        LoadDatasetFromCsvFile,

        // SELECT statement
        CreateNewDataset, 

        // predicate
        // if / thene / else
        // math formula
        // scalar value from SELECT statement
        SetDatasetColumn,

        SetScalar
    }

    public enum DatasetTypes
    {
        NA,
        SingleRow,
        MultipleRow
    }

    public class ModelBuild : IModifiable
    {
        public int ModelBuildID { get; set; }
        public ModelBuildTypes BuildType { get; set; }
        public DatasetTypes DatasetType { get; set; }

        public string DatasetName { get; set; }
        public string PropertyName { get; set; }




        public string BuildText { get; set; }

        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime ModifiedDate { get; set; }
    }

    public enum SpreadsheetBuildTypes
    {
        Dataset,
        SingleCell
    }

    public class SpreadsheetBuild
    {
        public SpreadsheetBuildTypes BuildType { get; set; }
        public string SheetName { get; set; }
        public string CellReference { get; set; }
        public string DatasetName { get; set; }
        public string PropertyName { get; set; }

    }
}
