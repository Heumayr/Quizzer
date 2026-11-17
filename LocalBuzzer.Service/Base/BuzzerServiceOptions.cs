using System;
using System.Collections.Generic;
using System.Text;

namespace LocalBuzzer.Service.Base
{
    public sealed class BuzzerServerOptions
    {
        /// <summary>Alle Adressen dieses Rechners - die Telefone kommen aus dem Heimnetz.</summary>
        public const string AnyAddress = "0.0.0.0";

        public int Port { get; set; } = 5000;
        public string BindAddress { get; set; } = AnyAddress; // LAN erreichbar
        public string WebRootPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "wwwroot");
    }
}