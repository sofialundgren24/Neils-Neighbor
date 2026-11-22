using System;
using System.Collections;
using UnityEngine;
#if NG_NOTIFICATIONS && UNITY_ANDROID
using Unity.Notifications.Android;
#endif
#if NG_NOTIFICATIONS && UNITY_IOS
using Unity.Notifications.iOS;
#endif

namespace NG.Notifications
{
    /// <summary>
    /// Lokala notiser på Android och iOS (paketet Mobile Notifications). Inga servrar behövs:
    /// spelet schemalägger själv "dina tulpaner är klara" när appen stängs, och tar bort dem när den öppnas.
    /// I editorn och på webben skrivs bara en rad i Console.
    /// Vad som schemaläggs bestäms av NotificationPlanner i spellogiken.
    /// </summary>
    public static class LocalNotifications
    {
        const string ChannelId = "ng_default";
        static bool _initialized;

        public static bool Supported =>
#if NG_NOTIFICATIONS && (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            true;
#else
            false;
#endif

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
#if NG_NOTIFICATIONS && UNITY_ANDROID && !UNITY_EDITOR
            AndroidNotificationCenter.Initialize();
            AndroidNotificationCenter.RegisterNotificationChannel(
                new AndroidNotificationChannel(ChannelId, "Neil's Neighbor", "Påminnelser från grannskapet", Importance.Default));
#endif
        }

        /// <summary>Frågar spelaren om lov att visa notiser (krävs på iOS och Android 13+). Kör som korutin.</summary>
        public static IEnumerator RequestPermission()
        {
            Initialize();
#if NG_NOTIFICATIONS && UNITY_ANDROID && !UNITY_EDITOR
            var request = new PermissionRequest();
            while (request.Status == PermissionStatus.RequestPending) yield return null;
#elif NG_NOTIFICATIONS && UNITY_IOS && !UNITY_EDITOR
            using (var request = new AuthorizationRequest(AuthorizationOption.Alert | AuthorizationOption.Badge | AuthorizationOption.Sound, true))
            {
                while (!request.IsFinished) yield return null;
            }
#else
            yield break;
#endif
        }

        /// <summary>Schemalägger en notis om "delay" från nu.</summary>
        public static void Schedule(string id, string title, string body, TimeSpan delay)
        {
            if (delay.TotalSeconds < 30) return;
            Initialize();
#if NG_NOTIFICATIONS && UNITY_ANDROID && !UNITY_EDITOR
            var n = new AndroidNotification(title, body, DateTime.Now + delay);
            AndroidNotificationCenter.SendNotification(n, ChannelId);
#elif NG_NOTIFICATIONS && UNITY_IOS && !UNITY_EDITOR
            iOSNotificationCenter.ScheduleNotification(new iOSNotification
            {
                Identifier = id,
                Title = title,
                Body = body,
                ShowInForeground = false,
                Trigger = new iOSNotificationTimeIntervalTrigger { TimeInterval = delay, Repeats = false }
            });
#else
            if (Application.isEditor) Debug.Log($"[Notis] om {delay:hh\\:mm\\:ss}: {title} – {body}");
#endif
        }

        /// <summary>Tar bort alla schemalagda och visade notiser (när spelaren öppnar appen).</summary>
        public static void CancelAll()
        {
#if NG_NOTIFICATIONS && UNITY_ANDROID && !UNITY_EDITOR
            Initialize();
            AndroidNotificationCenter.CancelAllScheduledNotifications();
            AndroidNotificationCenter.CancelAllDisplayedNotifications();
#elif NG_NOTIFICATIONS && UNITY_IOS && !UNITY_EDITOR
            iOSNotificationCenter.RemoveAllScheduledNotifications();
            iOSNotificationCenter.RemoveAllDeliveredNotifications();
            iOSNotificationCenter.ApplicationBadge = 0;
#endif
        }
    }
}
