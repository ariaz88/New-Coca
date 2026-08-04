# Quick Reference - V2 System

## 🔀 **Script Mapping:**

| Old Script | New Script | Status |
|------------|------------|---------|
| `Board.cs` | `BoardControllerV2.cs` | ✅ Ready |
| `Box.cs` | `BoxV2.cs` | ✅ Ready |
| N/A | `UniversalSodaTransferSystem.cs` | ✅ New Algorithm |

## 🎮 **Debug Controls:**

| Key | Action | Description |
|-----|--------|-------------|
| **T** | Log Board State | Shows all boxes and their soda colors |
| **C** | Show Cooldowns | Shows which boxes can transfer |

## ⚙️ **Setup Checklist:**

- [ ] Backup project
- [ ] Add `BoardControllerV2` to Board GameObject
- [ ] Disable old `Board` script
- [ ] Add `BoxV2` to Box prefab
- [ ] Disable old `Box` script
- [ ] Configure prefab references
- [ ] Test basic gameplay

## 🎯 **What's Fixed:**

| Bug | Old System | V2 System |
|-----|------------|-----------|
| Ping-Pong | ❌ Happens | ✅ Prevented by cooldown |
| Collision | ❌ Sodas hit each other | ✅ Sequential movement |
| Deadlock | ❌ Full boxes stuck | ✅ Smart priority system |
| Complexity | ❌ 25+ scenario methods | ✅ 1 universal algorithm |

## 🚨 **Emergency Rollback:**

If something breaks:
1. **Enable old scripts** (`Board`, `Box`)
2. **Disable new scripts** (`BoardControllerV2`, `BoxV2`)
3. **Game returns to original state**

## 📞 **Quick Support:**

**Not working?** Check Console for:
- `"BoardControllerV2 initialized"` ✅ 
- `"BoxV2 initialized"` ✅
- Error messages ❌

**Still having issues?** 
- Verify script enable/disable status
- Check prefab references are set
- Ensure singleton instances work correctly