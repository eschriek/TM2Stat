using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace tm
{
    public class Player 
    {
        public Player()
        {
            PlayedMaps = new List<Map>();
        }

        public int Id { get; set; }
        public string NickName { get; set; }
        public string InputDevice { get; set; }

        public List<Map> PlayedMaps { get; set; }
    }
}
