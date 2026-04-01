using System;
using System.Collections.Generic;
using System.Text;

namespace Spearing.Language.SqlConverter.Nodals
{

    public class NodalColumn
    {
        public string Alias { get; set; }
        public QueryNode RootNode { get; set; }
    }
}
