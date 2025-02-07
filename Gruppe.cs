using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ScreenZen
{
    public class Gruppe
    {
        public string Date { get; set; }
        public bool Aktiv { get; set; }
        public List<AppSZ> Apps { get; set; }
        public List<Website> Websites { get; set; }
    }

    public class AppSZ
    {
        public string Name { get; set; }
    }

    public class Website
    {
        public string Name { get; set; }
    }

    public class Config
    {
        public Dictionary<string, Gruppe> Gruppen { get; set; }
    }
}