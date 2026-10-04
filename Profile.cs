using System;
using System.IO;
using System.Text;

namespace FidoNet
{
    public class Profile
    {
        private readonly string _path;

        public FidoAddress Address { get; set; }
        public string Nick { get; set; } = "Anonymous";
        public string SysopName { get; set; } = "Sysop";
        public string SystemName { get; set; } = "MyFido";

        public Profile(string basePath)
        {
            Directory.CreateDirectory(basePath);
            _path = Path.Combine(basePath, "profile.txt");
            Load();
        }

        public void ChangeNick(string newNick)
        {
            if (string.IsNullOrWhiteSpace(newNick))
                throw new ArgumentException("Ник не может быть пустым");
            newNick = newNick.Trim();
            if (newNick.Length > 32) newNick = newNick.Substring(0, 32);
            Nick = newNick;
            Save();
        }

        public void ChangeSysop(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Имя сисопа не может быть пустым");
            SysopName = name.Trim();
            Save();
        }

        public void ChangeSystemName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Имя системы не может быть пустым");
            SystemName = name.Trim();
            Save();
        }

        public void SetAddress(FidoAddress addr)
        {
            Address = addr;
            Save();
        }

        private void Load()
        {
            if (!File.Exists(_path)) return;
            foreach (var line in File.ReadAllLines(_path, Encoding.UTF8))
            {
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                string key = line.Substring(0, eq).Trim();
                string val = line.Substring(eq + 1).Trim();
                switch (key)
                {
                    case "Nick": Nick = val; break;
                    case "SysopName": SysopName = val; break;
                    case "SystemName": SystemName = val; break;
                    case "Address":
                        try { Address = FidoAddress.Parse(val); } catch { }
                        break;
                }
            }
        }

        public void Save()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Nick={Nick}");
            sb.AppendLine($"SysopName={SysopName}");
            sb.AppendLine($"SystemName={SystemName}");
            if (Address != null)
                sb.AppendLine($"Address={Address}");
            File.WriteAllText(_path, sb.ToString(), Encoding.UTF8);
        }
    }
}   