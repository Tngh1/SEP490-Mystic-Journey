using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace MysticJourney.Tests.EditMode
{
    public sealed class SettingsBusinessRuleTests
    {
        private const string MasterVolumeKey = "mj_setting_master_vol";
        private const string DisplayModeKey = "mj_setting_display_mode";
        private const string ResolutionKey = "mj_setting_resolution";

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(MasterVolumeKey);
            PlayerPrefs.DeleteKey(DisplayModeKey);
            PlayerPrefs.DeleteKey(ResolutionKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void BR057_PersistentSettingsLoadFromLocalPlayerPrefs()
        {
            PlayerPrefs.SetFloat(MasterVolumeKey, 0.35f);
            PlayerPrefs.SetInt(DisplayModeKey, 2);
            PlayerPrefs.SetInt(ResolutionKey, 2);
            PlayerPrefs.Save();

            var service = GetSettingsService();
            Invoke(service, "SetDisplayMode", 1);
            Invoke(service, "SetResolution", 1);
            Invoke(service, "Load");

            Assert.That(GetProperty<float>(service, "MasterVolume"), Is.EqualTo(0.35f).Within(0.001f));
            Assert.That(GetProperty<int>(service, "DisplayModeIndex"), Is.EqualTo(1));
            Assert.That(GetProperty<int>(service, "ResolutionIndex"), Is.EqualTo(1));
            Assert.That(PlayerPrefs.HasKey(DisplayModeKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(ResolutionKey), Is.False);
        }

        [Test]
        public void BR057_GraphicsSettingsAreNotSavedToPlayerPrefs()
        {
            var service = GetSettingsService();
            Invoke(service, "SetDisplayMode", 2);
            Invoke(service, "SetResolution", 2);
            Invoke(service, "Save");

            Assert.That(PlayerPrefs.HasKey(DisplayModeKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(ResolutionKey), Is.False);
            Assert.That(GetProperty<int>(service, "DisplayModeIndex"), Is.EqualTo(2));
            Assert.That(GetProperty<int>(service, "ResolutionIndex"), Is.EqualTo(2));
        }

        [Test]
        public void BR057_VolumeLoadedFromLocalStorageIsNotServerGameplayState()
        {
            PlayerPrefs.SetFloat(MasterVolumeKey, 1.5f);
            PlayerPrefs.Save();

            var service = GetSettingsService();
            Invoke(service, "Load");

            Assert.That(GetProperty<float>(service, "MasterVolume"), Is.EqualTo(1.5f));
            Assert.That(service.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic),
                Has.None.Matches<FieldInfo>(field => field.FieldType.Name.Contains("Api", StringComparison.OrdinalIgnoreCase)));
        }

        private static object GetSettingsService()
        {
            var type = Type.GetType("SettingsService, Assembly-CSharp", throwOnError: true);
            return type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        }

        private static void Invoke(object target, string methodName)
            => target.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance).Invoke(target, null);

        private static void Invoke(object target, string methodName, object argument)
            => target.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance).Invoke(target, new[] { argument });

        private static T GetProperty<T>(object target, string propertyName)
            => (T)target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance).GetValue(target);
    }
}
