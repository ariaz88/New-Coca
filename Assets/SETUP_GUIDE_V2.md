# V2 System Setup Guide

## 🎯 **Safe Testing Approach**
You now have **V2 versions** of your scripts that can be tested alongside the old ones without breaking anything!

## 📁 **New Files Created:**
1. **`BoardControllerV2.cs`** - Replaces complex Board.cs logic
2. **`BoxV2.cs`** - Enhanced Box.cs with anti-ping-pong
3. **`UniversalSodaTransferSystem.cs`** - The new universal algorithm
4. **`TransferSystemDebugger.cs`** - Debug tools (optional)

## 🔧 **Setup Instructions:**

### Step 1: Prepare Your Scene
1. **Backup your project** (create a copy of the entire project folder)
2. **Open your main game scene**
3. **Find the GameObject that has the `Board` script attached**

### Step 2: Add V2 Scripts
1. **On the Board GameObject:**
   - Add `BoardControllerV2` component
   - Add `UniversalSodaTransferSystem` component (this will be added automatically)
   - Add `TransferSystemDebugger` component (optional, for testing)
   
2. **On the Board GameObject:**
   - **DISABLE** the old `Board` script (uncheck the checkbox)
   - **ENABLE** the new `BoardControllerV2` script

### Step 3: Update Box Prefabs
1. **Find your Box prefab(s)**
2. **Add `BoxV2` component to the prefab**
3. **DISABLE** the old `Box` script (uncheck the checkbox)
4. **ENABLE** the new `BoxV2` script

### Step 4: Configure References
1. **In `BoardControllerV2`:**
   - Set `nodePref` to your node prefab
   - Set `boxPref` to your box prefab
   
2. **In `BoxV2`:**
   - Set `topBox` to the top box prefab
   - Set `sodaPrefab` to your soda prefab
   - Set materials for highlights

## 🧪 **Testing Protocol:**

### Phase 1: Basic Functionality
1. **Play the game**
2. **Place a few boxes** and verify they appear correctly
3. **Check that sodas transfer** between adjacent boxes
4. **Verify no ping-pong effects** occur

### Phase 2: Debug Monitoring
1. **Press T** during gameplay to see board state
2. **Press C** during gameplay to see transfer cooldowns
3. **Watch the Console** for "BoardControllerV2 initialized" message

### Phase 3: Scenario Testing
Test the scenarios that were causing problems:
1. **Place boxes with same colors** (should transfer correctly)
2. **Create full boxes** (should not cause deadlocks)
3. **Test rapid box placement** (should not cause collisions)

## 🚨 **Troubleshooting:**

### If Nothing Happens:
- **Check BoardControllerV2 is enabled** and Board is disabled
- **Verify Box prefabs have BoxV2 enabled** and Box disabled
- **Look for error messages** in Console

### If Transfers Don't Work:
- **Enable TransferSystemDebugger**
- **Press T to check board state**
- **Check if UniversalSodaTransferSystem is attached**

### If Game Breaks:
- **Re-enable old scripts** (Board, Box)
- **Disable new scripts** (BoardControllerV2, BoxV2)
- **Everything should work as before**

## ✅ **Success Indicators:**

You'll know the V2 system is working when:
1. **No ping-pong effects** (sodas don't bounce back and forth)
2. **Smart transfers** (prioritizes completing boxes)
3. **No collision bugs** (sodas move one at a time)
4. **Console shows**: "BoardControllerV2 initialized with Universal Transfer System"

## 🔄 **Easy Rollback:**

If you want to go back to the old system:
1. **Enable old scripts**: `Board`, `Box`
2. **Disable new scripts**: `BoardControllerV2`, `BoxV2`
3. **Remove added components**: `UniversalSodaTransferSystem`, `TransferSystemDebugger`

## 🎊 **Next Steps:**

Once V2 system works perfectly:
1. **Test thoroughly with different scenarios**
2. **Keep both systems for a few days** to ensure stability
3. **Eventually remove old scripts** when confident
4. **Enjoy your bug-free soda sorting game!**

## 💡 **Key Benefits of V2 System:**

- ✅ **No scenario explosion** (1 algorithm vs 25+ methods)
- ✅ **No ping-pong effects** (cooldown system)
- ✅ **No collision bugs** (sequential transfers)
- ✅ **Smart prioritization** (completes boxes first)
- ✅ **Easy to maintain** (clean, readable code)
- ✅ **Self-optimizing** (automatic best transfer selection)

**Happy Testing! 🚀**