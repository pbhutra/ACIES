using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACIES.Model
{
    public class AragApiModel
    {
        public string AciesUrl { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string AragSftpHostAddress { get; set; }
        public int AragSftpPort { get; set; }
        public string AragUserName { get; set; }
        public string AragPassword { get; set; }
        public string AragUploadFolder { get; set; }
    }
}
