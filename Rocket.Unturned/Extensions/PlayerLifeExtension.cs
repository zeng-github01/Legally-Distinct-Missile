using Rocket.Core.Logging;
using SDG.NetTransport;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Logger = Rocket.Core.Logging.Logger;

namespace Rocket.Unturned.Extensions
{
    public static class PlayerLifeExtension
    {

        public static void serverModifyOxygen(this PlayerLife life, byte newOxygen)
        {
            ClientInstanceMethod<byte, byte, byte, byte, byte, bool, bool> SendLifeStats =
            typeof(PlayerLife).GetField("SendLifeStats", BindingFlags.NonPublic | BindingFlags.Static).GetValue(life) as
            ClientInstanceMethod<byte, byte, byte, byte, byte, bool, bool>;

            FieldInfo field = life.GetType().GetField("_oxygen", BindingFlags.NonPublic | BindingFlags.Instance);
            if (SendLifeStats != null && field != null)
            {
                field.SetValue(life, newOxygen);
                SendLifeStats.Invoke(life.GetNetId(), ENetReliability.Reliable, life.channel.owner.transportConnection,
                life.health, life.food, life.water, life.virus, life.oxygen, life.isBleeding,
                life.isBroken);
            }
        }
    }
}
