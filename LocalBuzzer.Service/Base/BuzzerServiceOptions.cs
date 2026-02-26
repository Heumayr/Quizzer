using System;
using System.Collections.Generic;
using System.Text;

namespace LocalBuzzer.Service.Base
{
    public sealed class BuzzerServerOptions
    {
        public int Port { get; set; } = 5000;
        public string BindAddress { get; set; } = "0.0.0.0"; // LAN erreichbar
        public string WebRootPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "wwwroot");
    }
}