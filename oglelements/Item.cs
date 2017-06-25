﻿using OGL.Base;
using OGL.Common;
using OGL.Items;
using OGL.Keywords;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using System.Xml.Xsl;
using XCalc;

namespace OGL
{
    [XmlInclude(typeof(Tool)),
    XmlInclude(typeof(Weapon)),
    XmlInclude(typeof(Armor)),
    XmlInclude(typeof(Shield)),
    XmlInclude(typeof(Pack)),
    XmlInclude(typeof(Scroll))]
    public class Item : IComparable<Item>, IHTML, OGLElement<Item>
    {
        [XmlArrayItem(Type = typeof(Keyword)),
        XmlArrayItem(Type = typeof(Versatile)),
        XmlArrayItem(Type = typeof(Range))]
        public List<Keyword> Keywords = new List<Keyword>();
        [XmlIgnore]
        public string filename;
        [XmlIgnore]
        protected static XmlSerializer serializer = new XmlSerializer(typeof(Item));
        [XmlIgnore]
        private static XslCompiledTransform transform = new XslCompiledTransform();
        [XmlIgnore]
        public bool autogenerated;
        public int StackSize { get; set; }
        [XmlIgnore]
        public static String Search = "";
        public String Name { get; set; }
        public String Description { get; set; }
        public Price Price { get; set; }
        public String Source { get; set; }
        public double Weight { get; set; }
        public string Unit { get; set; }
        public string SingleUnit { get; set; }
        [XmlIgnore]
        public Category Category { get; set; }
        [XmlIgnore]
        public Dictionary<string, bool> Matches;
        [XmlIgnore]
        static public Dictionary<String, Item> items = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);
        [XmlIgnore]
        static public Dictionary<String, Item> simple = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);
        [XmlIgnore]
        static public Dictionary<String, List<Item>> ItemLists = new Dictionary<string, List<Item>>(StringComparer.OrdinalIgnoreCase);
        [XmlIgnore]
        public bool ShowSource { get; set; } = false;
        [XmlIgnore]
        public Bitmap Image
        {
            set
            { // serialize
                if (value == null) ImageData = null;
                else using (MemoryStream ms = new MemoryStream())
                {
                    value.Save(ms, ImageFormat.Png);
                    ImageData = ms.ToArray();
                }
            }
            get
            { // deserialize
                if (ImageData == null)
                {
                    return null;
                }
                else
                {
                    using (MemoryStream ms = new MemoryStream(ImageData))
                    {
                        return new Bitmap(ms);
                    }
                }
            }
        }

        public byte[] ImageData { get; set; }
        public void register(String file)
        {
            filename = file;
            foreach (Keyword kw in Keywords) kw.check();
            string full = Name + " " + ConfigManager.SourceSeperator + " " + Source;
            if (items.ContainsKey(full)) throw new Exception("Duplicate Item: " + full);
            items.Add(full, this);
            if (simple.ContainsKey(Name))
            {
                simple[Name].ShowSource = true;
                ShowSource = true;
            }
            else simple.Add(Name, this);
        }
        public Item()
        {
            Price = new Price();
            Weight = 0;
            autogenerated = false;
            StackSize = 1;
            Category = Category.Make();
            Source = ConfigManager.DefaultSource;
        }
        protected Item(String name)
        {
            Name = name;
            Description = "Missing Entry";
            Price = new Price();
            Weight = 0;
            autogenerated = true;
            StackSize = 1;
            Category = Category.Make();
            Source = "Autogenerated Entry";
            ShowSource = true;
        }
        public Item(String name, String description, Price price, double weight, int stacksize = 1, Keyword kw1 = null, Keyword kw2 = null, Keyword kw3 = null, Keyword kw4 = null, Keyword kw5 = null, Keyword kw6 = null, Keyword kw7 = null)
        {
            Name = name;
            Description = description;
            Price = price;
            Weight = weight;
            autogenerated = false;
            StackSize = stacksize;
            Category = Category.Make();
            Source = ConfigManager.DefaultSource;
            Keywords = new List<Keyword>() { kw1, kw2, kw3, kw4, kw5, kw6, kw7 };
            Keywords.RemoveAll(kw => kw == null);
            register(null);
        }
        public Tool asTool()
        {
            if (this is Tool)
            {
                return (Tool)this;
            }
            else
            {
                if (autogenerated)
                {
                    return new Tool(this);
                }
                else
                {
                    throw new Exception("Tried to use " + Name + " as a tool when it is not a tool");
                }
            }
        }
        public bool Test()
        {
            if (Name != null && Name.ToLowerInvariant().Contains(Search)) return true;
            if (Description != null && Description.ToLowerInvariant().Contains(Search)) return true;
            if (Keywords != null && Keywords.Exists(k => k.Name == Search)) return true;
            return false;
        }
        public static Item Get(String name, string sourcehint)
        {
            if (name.Contains(ConfigManager.SourceSeperator))
            {
                if (items.ContainsKey(name)) return items[name];
                if (Spell.spells.ContainsKey(name)) return new Scroll(Spell.Get(name, sourcehint));
                name = SourceInvariantComparer.NoSource(name);
            }
            if (sourcehint != null && items.ContainsKey(name + " " + ConfigManager.SourceSeperator + " " + sourcehint)) return items[name + " " + ConfigManager.SourceSeperator + " " + sourcehint];
            if (sourcehint != null && Spell.spells.ContainsKey(name + " " + ConfigManager.SourceSeperator + " " + sourcehint)) return new Scroll(Spell.Get(name + " " + ConfigManager.SourceSeperator + " " + sourcehint, sourcehint));
            if (simple.ContainsKey(name)) return simple[name];
            if (Spell.simple.ContainsKey(name)) return new Scroll(Spell.Get(name, sourcehint));
            return new Item(name);
        }
        public static void ExportAll()
        {
            foreach (Item i in items.Values)
            {
                FileInfo file = SourceManager.getFileName(i.Name, i.Source, Path.Combine(ConfigManager.Directory_Items, i.Category.makePath()));
                file.Directory.Create();
                using (TextWriter writer = new StreamWriter(file.FullName)) serializer.Serialize(writer, i);
            }
        }
        public static void ImportAll()
        {
            items.Clear();
            ItemLists.Clear();
            simple.Clear();
            var files = SourceManager.EnumerateFiles(ConfigManager.Directory_Items);

            foreach (var f in files)
            {
                try
                {
                    Uri source = new Uri(SourceManager.getDirectory(f.Value, ConfigManager.Directory_Items).FullName);
                    Uri target = new Uri(f.Key.DirectoryName);
                    using (TextReader reader = new StreamReader(f.Key.FullName))
                    {
                        Item s = (Item)serializer.Deserialize(reader);
                        s.Category = Category.Make(source.MakeRelativeUri(target));
                        s.Source = f.Value;
                        s.register(f.Key.FullName);
                    }
                }
                catch (Exception e)
                {
                    ConfigManager.LogError("Error reading " + f.ToString(), e);
                }
            }
        }
        public virtual String toHTML()
        {
            try
            {
                if (transform.OutputSettings == null) transform.Load(ConfigManager.Transform_Items.FullName);
                using (MemoryStream mem = new MemoryStream())
                {
                    serializer.Serialize(mem, this);
                    ConfigManager.RemoveDescription(mem);
                    mem.Seek(0, SeekOrigin.Begin);
                    XmlReader xr = XmlReader.Create(mem);
                    using (StringWriter textWriter = new StringWriter())
                    {
                        using (XmlWriter xw = XmlWriter.Create(textWriter))
                        {
                            transform.Transform(xr, xw);
                            return textWriter.ToString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return "<html><body><b>Error generating output:</b><br>" + ex.Message + "<br>" + ex.InnerException + "<br>" + ex.StackTrace + "</body></html>";
            }
        }
        public override string ToString()
        {
            string n;
            if (StackSize == 1 && SingleUnit != null) n = Name + " (" + SingleUnit + ")";
            else if (Unit == null) n = Name;
            else n = Name + " (" + StackSize + (Unit == null ? "" : " " + Unit) + ")";
            if (ShowSource || ConfigManager.AlwaysShowSource) return n + " " + ConfigManager.SourceSeperator + " " + Source;
            return n;
        }
        public int CompareTo(Item other)
        {
            return Name.CompareTo(other.Name);
        }
        public static IOrderedEnumerable<Item> Subsection(Category section)
        {
            if (Search == "")
            {
                if (section == null) return (from i in items.Values orderby i select i);
                else return (from i in items.Values where i.Category == section orderby i select i);
            }
            else
            {
                Search = Search.ToLowerInvariant();
                if (section == null) return (from i in items.Values where i.Test() orderby i select i);
                else return (from i in items.Values where i.Category == section && i.Test() orderby i select i);
            }
        }
        public static IOrderedEnumerable<Category> Section()
        {
            if (Search == "")
            {
                return Category.Section();
            }
            else
            {
                Search = Search.ToLowerInvariant();
                return (from i in items.Values where i.Test() select i.Category).Distinct().Where(i => i.ToString() != "Items").OrderBy(i => i);
            }
        }
        public bool save(Boolean overwrite)
        {

            Item o = null;
            Name = Name.Replace(ConfigManager.SourceSeperator, '-');
            if (items.ContainsKey(Name + " " + ConfigManager.SourceSeperator + " " + Source)) o = items[Name + " " + ConfigManager.SourceSeperator + " " + Source];
            if (o != null && !o.autogenerated && o.Category.Path != Category.Path)
            {
                throw new Exception("Item needs a unique name");
            }
            FileInfo file = SourceManager.getFileName(Name, Source, Path.Combine(ConfigManager.Directory_Items, Category.makePath()));
            if (file.Exists && (filename == null || !filename.Equals(file.FullName)) && !overwrite) return false;
            using (TextWriter writer = new StreamWriter(file.FullName)) serializer.Serialize(writer, this);
            this.filename = file.FullName;
            return true;
        }
        public Item clone()
        {
            using (MemoryStream mem = new MemoryStream())
            {
                serializer.Serialize(mem, this);
                mem.Seek(0, SeekOrigin.Begin);
                Item r = (Item)serializer.Deserialize(mem);
                r.filename = filename;
                r.Category = Category;
                r.Name = Name;
                return r;
            }
        }
        public static string cleanname(string path)
        {
            string cat = path;
            if (!cat.StartsWith(new FileInfo(ConfigManager.Directory_Items).Directory.Name)) cat = Path.Combine(new FileInfo(ConfigManager.Directory_Items).Directory.Name, path);
            cat = cat.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            //if (!Collections.ContainsKey(cat)) Collections.Add(cat, new FeatureCollection());
            return cat;
        }
        private static bool matchesKW(string kw, string kw2)
        {
            return kw.Replace('-', '_').Equals(kw2.Replace('-', '_'), StringComparison.InvariantCultureIgnoreCase);
        }
        public static List<Item> filterPreview(string expression)
        {
            if (expression == null || expression == "") expression = "true";
            if (Item.ItemLists.ContainsKey(expression)) return new List<Item>(Item.ItemLists[expression]);
            try
            {
                Expression ex = new Expression(ConfigManager.fixQuotes(expression));
                Item current = null;
                ex.EvaluateParameter += delegate (string name, ParameterArgs args)
                {
                    name = name.ToLowerInvariant();
                    if (name == "category") args.Result = current.Category.Path;
                    else if (name == "weapon") args.Result = (current is Weapon);
                    else if (name == "armor") args.Result = (current is Armor);
                    else if (name == "shield") args.Result = (current is Shield);
                    else if (name == "tool") args.Result = (current is Tool);
                    else if (name == "name") args.Result = current.Name.ToLowerInvariant();
                    else if (current.Keywords.Count > 0 && current.Keywords.Exists(k => matchesKW(k.Name, name))) args.Result = true;
                    else args.Result = false;
                };
                List<Item> res = new List<Item>();
                foreach (Item f in Item.items.Values)
                {
                    current = f;
                    object o = ex.Evaluate();
                    if (o is Boolean && (Boolean)o) res.Add(current);

                }
                res.Sort();
                Item.ItemLists[expression] = res;
                return res;
            }
            catch (Exception e)
            {
                throw new Exception("Error while evaluating expression " + expression, e);
            }
        }
    }
}
