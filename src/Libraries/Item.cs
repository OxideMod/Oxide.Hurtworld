﻿using Oxide.Core.Libraries;
using UnityEngine;

namespace Oxide.Game.Hurtworld.Libraries
{
    public class Item : Library
    {
        // Game references
        internal static readonly GlobalItemManager ItemManager = GlobalItemManager.Instance;

        /// <summary>
        /// Gets item based on item ID
        /// </summary>
        /// <param name="itemId"></param>
#if ITEMV2
        public static ItemObject GetItem(int itemId) => ItemManager.GetItem(itemId);
#else
        public static Assets.Scripts.Core.IItem GetItem(int itemId) => ItemManager.GetItem(itemId);
#endif

        #region Object Control

#if ITEMV2
        public void DestroyObject(GameObject obj) => HNetworkManager.Instance.NetDestroy(obj.HNetworkView());
#else
        public void DestroyObject(GameObject obj) => HNetworkManager.Instance.NetDestroy(obj.uLinkNetworkView());
#endif

        public void MoveObject(GameObject obj, Vector3 destination) => obj.GetComponent<Transform>().position = destination;

#if ITEMV2
        public GameObject SpawnObject(NetworkInstantiateConfig prefab, Vector3 position, Quaternion rotation)
        {
            return HNetworkManager.Instance.NetInstantiate(uLink.NetworkPlayer.server, prefab, position, rotation, GameManager.GetSceneTime());
        }
#else
        public GameObject SpawnObject(string obj, Vector3 position, Quaternion angle)
        {
            return HNetworkManager.Instance.NetInstantiate(uLink.NetworkPlayer.server, obj, position, angle, GameManager.GetSceneTime());
        }
#endif

        public GameObject ObjectByName(string partialName)
        {
            GameObject[] gos = Object.FindObjectsOfType<GameObject>();
            foreach (GameObject g in gos)
            {
                if (g.name.Contains(partialName))
                {
                    return g;
                }
            }

            return null;
        }

        public void AttachComponent(string objectName, Component component)
        {
            GameObject[] gos = Object.FindObjectsOfType<GameObject>();
            foreach (GameObject g in gos)
            {
                if (!g.activeInHierarchy)
                {
                    continue;
                }

                if (g.name.Contains(objectName))
                {
                    g.AddComponent(component.GetType());
                }
            }
        }

        #endregion Object Control
    }
}
