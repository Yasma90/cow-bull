using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CowBullClient.Model
{
    [Serializable]
    public class Mensaje_Data
    {
        private static long serialVersionUID = 9178463713495654837L;
        public int Action;
        public string texto;
        public bool last_msg = false;
    }
}
