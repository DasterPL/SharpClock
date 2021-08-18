using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace SharpClock
{
    class Config
    {
        string file = "config.xml";
        XmlDocument xml;
        public Config()
        {
            xml = new XmlDocument();
            try
            {
                xml.Load(file);
            }
            catch (Exception)
            {
                xml.LoadXml("<?xml version=\"1.0\" encoding=\"UTF-8\"?><config><properties><screen brightness = \"10\" /><animatedSwitching enabled=\"false\" /><nightmode enable = \"False\" /></properties><modules></modules</config>");
                xml.Save(file);
            }
        }
        public class Module
        {
            public string Class { get; private set; }
            public bool Start { get; private set; }
            public Dictionary<string, string> Params { get; private set; }

            public Module(XmlElement module)
            {
                Class = module.GetAttribute("class");
                Start = bool.Parse(module.GetAttribute("start"));

                var Params = new Dictionary<string, string>();
                foreach (XmlAttribute param in module["params"].Attributes)
                {
                    Params.Add(param.Name, param.Value);
                }
                this.Params = Params;
            }
            public override string ToString()
            {
                return Class;
            }
        }
        public Module[] Modules
        {
            get
            {
                var modules = new List<Module>();
                foreach (XmlElement module in xml.DocumentElement["modules"])
                {
                    modules.Add(new Module(module));
                }
                return modules.ToArray();
            }
        }
        public Module GetModule(string name)
        {
            foreach (XmlElement module in xml.DocumentElement["modules"])
            {
                if (module.GetAttribute("class") == name)
                    return new Module(module);
            }
            return null;
        }
        public string[] ModuleOrder
        {
            get
            {
                List<string> tmp = new List<string>();
                foreach (XmlElement module in xml.DocumentElement["modules"])
                {
                    tmp.Add(module.GetAttribute("class"));
                }
                return tmp.ToArray();
            }
        }
        public void EditModules(PixelModule[] modules)
        {
            //xml.DocumentElement["modules"].InnerText = "";            
            foreach (var module in modules)
            {
                XmlElement m = null;
                foreach (XmlElement moduleElement in xml.DocumentElement["modules"])
                {
                    if (moduleElement.GetAttribute("class") == module.Name)
                        m = moduleElement;
                }
                if (m == null)
                {
                    Logger.Log("Coś nie pykło");
                    break;
                }
                m.SetAttribute("start", module.IsRunning.ToString());

                XmlElement p = m["params"];

                foreach (var prop in module.GetType().GetProperties())
                {
                    if (prop.Name == "Name" || prop.Name == "IsRunning" || prop.Name == "Icon")
                    {
                        continue;
                    }
                    //else if (prop.PropertyType.BaseType == typeof(Enum))
                    //{
                    //    p.SetAttribute(prop.PropertyType.Name, prop.GetValue(module).ToString());
                    //}
                    else if (prop.PropertyType == typeof(Color))
                    {
                        var c = (Color)prop.GetValue(module);
                        p.SetAttribute(prop.Name, $"#{c.R.ToString("X2")}{c.G.ToString("X2")}{c.B.ToString("X2")}");
                    }
                    else
                    {
                        p.SetAttribute(prop.Name, prop.GetValue(module).ToString());
                    }
                }
                m.AppendChild(p);
                xml.DocumentElement["modules"].AppendChild(m);
            }
            xml.Save(file);
        }
        public void CreateModule(PixelModule module)
        {
            XmlElement m = xml.CreateElement(string.Empty, "module", string.Empty);

            Logger.Log(ConsoleColor.Yellow, $"Module Name: {module.Name} Start: True");
            m.SetAttribute("class", module.Name);
            m.SetAttribute("start", "True");

            XmlElement p = xml.CreateElement(string.Empty, "params", string.Empty);
            foreach (var prop in module.GetType().GetProperties())
            {
                try
                {
                    Logger.Log(ConsoleColor.Yellow, $"Prop: {prop.Name} Value: {prop.GetValue(module).ToString()}");
                    if (prop.Name == "Name" || prop.Name == "IsRunning" || prop.Name == "Icon")
                    {
                        continue;
                    }
                    else if (prop.PropertyType == typeof(Color))
                    {
                        var c = (Color)prop.GetValue(module);
                        p.SetAttribute(prop.Name, $"#{c.R.ToString("X2")}{c.G.ToString("X2")}{c.B.ToString("X2")}");
                    }
                    else
                    {
                        p.SetAttribute(prop.Name, prop.GetValue(module).ToString());
                    }
                }
                catch (Exception)
                {
                    Logger.Log(ConsoleColor.Yellow, $"Prop: {prop.Name} Value: ", ConsoleColor.Red, "NULL");
                }


            }

            m.AppendChild(p);
            xml.DocumentElement["modules"].AppendChild(m);
            xml.Save(file);
        }
        public void RemoveModule(string name)
        {
            XmlElement tmp = null;
            foreach (XmlElement module in xml.DocumentElement["modules"])
            {
                if (module.GetAttribute("class") == name)
                    tmp = module;
            }
            xml.DocumentElement["modules"].RemoveChild(tmp);
            xml.Save(file);
        }
        public void SortModules(string[] names)
        {
            List<XmlElement> tmp = new List<XmlElement>();
            foreach (XmlElement module in xml.DocumentElement["modules"])
            {
                tmp.Add(module);
            }
            xml.DocumentElement["modules"].InnerText = "";
            foreach (string name in names)
            {
                xml.DocumentElement["modules"].AppendChild(tmp.Find((x) => x.GetAttribute("class") == name));
            }
            xml.Save(file);
        }
        public byte Brightness
        {
            get => byte.Parse(xml.DocumentElement["properties"]["screen"].GetAttribute("brightness"));
            set
            {
                xml.DocumentElement["properties"]["screen"].SetAttribute("brightness", value.ToString());
                xml.Save(file);
            }
        }
        public bool AnimatedSwitching
        {
            get => bool.Parse(xml.DocumentElement["properties"]["animatedSwitching"].GetAttribute("enabled"));
            set
            {
                xml.DocumentElement["properties"]["animatedSwitching"].SetAttribute("enabled", value.ToString());
                xml.Save(file);
            }
        }
    }
}
