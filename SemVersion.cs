using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HecticEscape
{
    public class SemVersion : IComparable<SemVersion>
    {
        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }
        public string? PreRelease { get; }

        private static readonly List<string> PreReleaseOrder = new() { "alpha", "beta", "rc" };

        public SemVersion(string version)
        {
            version = version.TrimStart('v', 'V');
            var dashSplit = version.Split('-', 2);
            var versionParts = dashSplit[0].Split('.');
            if (versionParts.Length < 3)
                throw new ArgumentException("Version benötigt mindestens Major.Minor.Patch");

            Major = int.Parse(versionParts[0]);
            Minor = int.Parse(versionParts[1]);
            Patch = int.Parse(versionParts[2]);

            PreRelease = dashSplit.Length > 1 ? dashSplit[1] : null;
        }

        public int CompareTo(SemVersion? other)
        {
            if (other == null) return 1;

            int majorDiff = Major.CompareTo(other.Major);
            if (majorDiff != 0) return majorDiff;

            int minorDiff = Minor.CompareTo(other.Minor);
            if (minorDiff != 0) return minorDiff;

            int patchDiff = Patch.CompareTo(other.Patch);
            if (patchDiff != 0) return patchDiff;

            if (PreRelease == null && other.PreRelease == null)
                return 0;
            if (PreRelease == null)
                return 1;
            if (other.PreRelease == null)
                return -1;

            int thisIndex = PreReleaseOrder.IndexOf(PreRelease.ToLower());
            int otherIndex = PreReleaseOrder.IndexOf(other.PreRelease.ToLower());

            if (thisIndex == -1) thisIndex = int.MaxValue;
            if (otherIndex == -1) otherIndex = int.MaxValue;

            return thisIndex.CompareTo(otherIndex);
        }

        public override string ToString()
        {
            return $"{Major}.{Minor}.{Patch}" + (PreRelease != null ? $"-{PreRelease}" : "");
        }
    }

}
