using System;
using System.Collections.Generic;

namespace RailRider
{
    // Shared rifle support. Content mods own their profile values, clips and asset directories.
    public sealed class RifleProfile
    {
        public const string RailRiderId = "railrider";
        public string Id, SoundPrefix = "", AudioDirectory;
        public float MuzzleSpeed, ProjectileMass, DragK, PowerBoost, MaxDeltaV, MaxTargetMass, ZeroRange, Radius;
        public float EyeRelief, RecoilBack, RecoilFlip, RecoilTwist, RecoilTime, AudioRange;
        public bool Rattles;
        static readonly Dictionary<string, RifleProfile> profiles = new Dictionary<string, RifleProfile>();
        public static readonly Dictionary<string, List<GltfClip>> Clips = new Dictionary<string, List<GltfClip>>();
        static readonly RifleProfile railRider = new RifleProfile { Id = RailRiderId };

        public static void Register(RifleProfile profile, List<GltfClip> clips)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.Id) || profile.Id == RailRiderId ||
                profile.MuzzleSpeed <= 0f || profile.ProjectileMass <= 0f || profile.Radius <= 0f || profile.RecoilTime <= 0f)
                throw new ArgumentException("Invalid external rifle profile");
            if (profiles.ContainsKey(profile.Id)) throw new InvalidOperationException("Rifle profile already registered: " + profile.Id);
            profiles.Add(profile.Id, profile);
            Clips[profile.Id] = clips;
        }

        public static RifleProfile Get(string id)
        {
            if (!string.IsNullOrEmpty(id) && id != RailRiderId)
            {
                if (profiles.TryGetValue(id, out var profile)) return profile;
                throw new InvalidOperationException("Rifle profile not registered: " + id);
            }
            var s = Main.S; var r = railRider;
            r.MuzzleSpeed = s.muzzleSpeed; r.ProjectileMass = s.bbMass; r.DragK = s.dragK;
            r.PowerBoost = s.powerBoost; r.MaxDeltaV = s.maxDeltaV; r.MaxTargetMass = s.maxTargetMass;
            r.ZeroRange = s.zeroRange; r.Radius = .006f; r.EyeRelief = s.eyeRelief;
            r.RecoilBack = .07f; r.RecoilFlip = 6f; r.RecoilTwist = 0f; r.RecoilTime = .22f;
            r.AudioRange = 20f; r.Rattles = true;
            return r;
        }
    }
}
