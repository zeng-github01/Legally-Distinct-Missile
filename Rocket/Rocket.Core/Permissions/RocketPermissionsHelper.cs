using System;
using System.Collections.Generic;
using System.Linq;
using Rocket.API;
using Rocket.API.Serialisation;
using Rocket.Core.Assets;

namespace Rocket.Core.Permissions
{
    internal class RocketPermissionsHelper
    {
        internal Asset<RocketPermissions> permissions;

        public RocketPermissionsHelper(Asset<RocketPermissions> permissions)
        {
            this.permissions = permissions;
        }

        public List<RocketPermissionsGroup> GetGroupsByIds(List<string> ids) =>
            permissions.Instance.Groups
            .Where(g => ids.Exists(x => x.Equals(g.Id, StringComparison.InvariantCultureIgnoreCase)))
            .OrderBy(x => x.Priority).ToList();

        public List<string> GetParentGroups(string parentGroup, string currentGroup)
        {
            var allGroups = new List<string>();
            var group = permissions.Instance.Groups.OrderBy(x => x.Priority)
                .FirstOrDefault(g => string.Equals(g.Id, parentGroup, StringComparison.CurrentCultureIgnoreCase));

            if (group == null || string.Equals(group.Id, currentGroup, StringComparison.CurrentCultureIgnoreCase))
            {
                return allGroups;
            }

            allGroups.Add(group.Id);
            allGroups.AddRange(GetParentGroups(group.ParentGroup, currentGroup));

            return allGroups;
        }

        public bool HasPermission(IRocketPlayer player, List<string> requestedPermissions)
        {
            if (player.IsAdmin)
            {
                return true;
            }

            var applyingPermissions = GetPermissions(player, requestedPermissions);

            return applyingPermissions.Count != 0;
        }

        internal RocketPermissionsGroup GetGroup(string groupId) => permissions.Instance.Groups.OrderBy(x => x.Priority)
            .FirstOrDefault(g => string.Equals(g.Id, groupId, StringComparison.CurrentCultureIgnoreCase));

        internal RocketPermissionsProviderResult RemovePlayerFromGroup(string groupId, IRocketPlayer player)
        {
            var g = GetGroup(groupId);
            if (g == null)
                return RocketPermissionsProviderResult.GroupNotFound;

            if (g.Members.Find(m => m == player.Id) == null)
                return RocketPermissionsProviderResult.PlayerNotFound;

            g.Members.Remove(player.Id);
            SaveGroup(g);
            return RocketPermissionsProviderResult.Success;
        }

        internal RocketPermissionsProviderResult AddPlayerToGroup(string groupId, IRocketPlayer player)
        {
            var g = GetGroup(groupId);
            if (g == null)
                return RocketPermissionsProviderResult.GroupNotFound;

            if (g.Members.Find(m => m == player.Id) != null)
                return RocketPermissionsProviderResult.DuplicateEntry;

            g.Members.Add(player.Id);
            SaveGroup(g);
            return RocketPermissionsProviderResult.Success;
        }

        internal RocketPermissionsProviderResult DeleteGroup(string groupId)
        {
            var g = GetGroup(groupId);
            if (g == null) return RocketPermissionsProviderResult.GroupNotFound;

            permissions.Instance.Groups.Remove(g);
            permissions.Save();
            return RocketPermissionsProviderResult.Success;
        }

        internal RocketPermissionsProviderResult SaveGroup(RocketPermissionsGroup group)
        {
            var i = permissions.Instance.Groups.FindIndex(gr => gr.Id == group.Id);
            if (i < 0) return RocketPermissionsProviderResult.GroupNotFound;
            permissions.Instance.Groups[i] = group;
            permissions.Save();
            return RocketPermissionsProviderResult.Success;
        }

        internal RocketPermissionsProviderResult AddGroup(RocketPermissionsGroup group)
        {
            var i = permissions.Instance.Groups.FindIndex(gr => gr.Id == group.Id);
            if (i != -1) return RocketPermissionsProviderResult.DuplicateEntry;
            permissions.Instance.Groups.Add(group);
            permissions.Save();
            return RocketPermissionsProviderResult.Success;
        }

        public List<RocketPermissionsGroup> GetGroups(IRocketPlayer player, bool includeParentGroups)
        {
            // get player groups
            var groups = permissions.Instance?.Groups?.OrderBy(x => x.Priority)
                                                      .Where(g => g.Members.Contains(player.Id))
                                                      .ToList() ?? new List<RocketPermissionsGroup>();

            // get first default group
            var defaultGroup = permissions.Instance?.Groups?.OrderBy(x => x.Priority)
                .FirstOrDefault(g => string.Equals(g.Id, permissions.Instance.DefaultGroup, StringComparison.CurrentCultureIgnoreCase));

            // if exists, add to player groups
            if (defaultGroup != null)
            {
                groups.Add(defaultGroup);
            }

            // if requested, return list without parent groups
            if (!includeParentGroups)
            {
                return groups.Distinct().OrderBy(x => x.Priority).ToList();
            }

            // add parent groups
            var parentGroups = new List<RocketPermissionsGroup>();
            foreach (var group in groups)
            {
                parentGroups.AddRange(GetGroupsByIds(GetParentGroups(group.ParentGroup, group.Id)));
            }
            groups.AddRange(parentGroups);

            return groups.Distinct().OrderBy(x => x.Priority).ToList();
        }

        public List<Permission> GetPermissions(IRocketPlayer player)
        {
            var result = new List<Permission>();

            var playerGroups = GetGroups(player, true);
            playerGroups.Reverse(); // because we need desc ordering

            foreach (var group in playerGroups)
            {
                foreach (var permission in group.Permissions)
                {
                    if (permission.Name.StartsWith("-", StringComparison.Ordinal))
                    {
                        var substringed = permission.Name.Substring(1);

                        result.RemoveAll(x => string.Equals(x.Name, substringed, StringComparison.InvariantCultureIgnoreCase));
                        continue;
                    }

                    result.RemoveAll(x => string.Equals(x.Name, permission.Name, StringComparison.InvariantCultureIgnoreCase));
                    result.Add(permission);
                }
            }

            return result;
        }

        public List<Permission> GetPermissions(IRocketPlayer player, List<string> requestedPermissions)
        {
            var playerPermissions = GetPermissions(player);

            var applyingPermissions = playerPermissions
                .Where(p => requestedPermissions.Exists(x => string.Equals(x, p.Name, StringComparison.InvariantCultureIgnoreCase)))
                .ToList();

            if (playerPermissions.Exists(p => p.Name.Equals("*", StringComparison.Ordinal)))
            {
                applyingPermissions.Add(new Permission("*"));
            }

            foreach (var permission in playerPermissions)
            {
                if (!permission.Name.EndsWith(".*", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var requestedPermission in requestedPermissions)
                {
                    var dotIndex = requestedPermission.LastIndexOf(".", StringComparison.InvariantCultureIgnoreCase);
                    var baseRequested = dotIndex > 0 ? requestedPermission.Substring(0, dotIndex) : requestedPermission;

                    dotIndex = permission.Name.LastIndexOf(".", StringComparison.InvariantCultureIgnoreCase);
                    var basePlayer = dotIndex > 0 ? permission.Name.Substring(0, dotIndex) : permission.Name;

                    if (string.Equals(basePlayer, baseRequested, StringComparison.InvariantCultureIgnoreCase)) { applyingPermissions.Add(permission); }
                }
            }

            return applyingPermissions.Distinct().ToList();
        }

    }
}