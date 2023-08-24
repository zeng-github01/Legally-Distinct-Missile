using Rocket.Core.Logging;
using SDG.NetTransport;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Logger = Rocket.Core.Logging.Logger;

namespace Rocket.Unturned.Extensions
{
    public static class PlayerLifeExtension
    {
        private static readonly ClientInstanceMethod<sbyte> SendModifyOxygen = ClientInstanceMethod<sbyte>.Get(typeof(PlayerLife), "simulatedModifyOxygen");

        public static void serverModifyOxygen(this PlayerLife life ,float delta)
        {
           try
            {
                sbyte num = MathfEx.RoundAndClampToSByte(delta);
                if (num != 0)
                {
                    life.simulatedModifyOxygen(num);
                    if (!life.channel.isOwner)
                    {
                        SendModifyOxygen.Invoke(life.GetNetId(), ENetReliability.Reliable, life.channel.GetOwnerTransportConnection(), num);
                    }
                }
            }
            catch(Exception e)
            {
               if(SendModifyOxygen == null)
                {
                    Logger.LogWarning("execute client mothod SendModifyOxyegn is null");
                }
                Logger.LogException(e,"ServerModifyOxygen occur error");
            }
        }
    }
}
