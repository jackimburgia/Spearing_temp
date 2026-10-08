using System;
using System.Collections.Generic;
using System.Text;

namespace Spearing.Utilities.Entities.EntitiesUtilities
{
    public interface IModifiable
    {
        string CreatedBy { get; set; }
        DateTime CreatedDate { get; set; }
        string ModifiedBy { get; set; }
        DateTime ModifiedDate { get; set; }
    }
}
