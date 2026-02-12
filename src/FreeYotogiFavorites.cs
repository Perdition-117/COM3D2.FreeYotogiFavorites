using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using I2PluginLocalization;
using UnityEngine;
using Yotogis;

namespace FreeYotogiFavorites;

[BepInPlugin("net.perdition.com3d2.freeyotogifavorites", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
class FreeYotogiFavorites : BaseUnityPlugin {
	private const string TypeCategoryViewer = "タイプ";
	private const string CategoryViewer = "カテゴリー";
	private const string SkillSelectViewer = "スキル";
	private const int MaxRecentSkills = 20;

	private static readonly Color HoverColor = Color.white;
	private static readonly Color FavoriteSkillColor = new(218 / 255f, 218 / 255f, 187 / 255f);
	private static readonly Color FavoriteSkillColorHover = new(255 / 255f, 255 / 255f, 224 / 255f);

	private static readonly string PluginPath = Path.Combine(Paths.PluginPath, PluginInfo.PLUGIN_NAME);
	private static readonly PluginLocalization Localization = PluginLocalization.Load(Path.Combine(PluginPath, "localization"), PluginInfo.PLUGIN_NAME);

	private static readonly FreeSkillSelect.ButtonData TypeButtonData;
	private static readonly FreeSkillSelect.ButtonData CategoryButtonData;

	private static ConfigFile _config;
	private static ConfigEntry<bool> _showRecentSkills;
	private static ConfigEntry<List<int>> _favoriteSkills;
	private static ConfigEntry<List<int>> _recentSkills;

	private static YotogiManager.PlayingSkillData _activeSkill;

	static FreeYotogiFavorites() {
		TypeButtonData = new() {
			root_type = "favorites",
			name = "Favorite", // needs to be different than the localized text in order to trigger text spacing adjustment
			nameTerm = Localization.GetTermKey("Favorites"),
			children_list = new(),
		};

		CategoryButtonData = new() {
			parent = TypeButtonData,
			name = "MAX", // random Yotogi.Category value to avoid an error
			nameTerm = Localization.GetTermKey("Favorites"),
			children_list = new(),
		};
		
		TypeButtonData.children_list.Add(CategoryButtonData);
	}

	private void Awake() {
		var typeConverter = new TypeConverter {
			ConvertToString = (o, type) => string.Join(",", ((List<int>)o).Select(e => e.ToString()).ToArray()),
			ConvertToObject = (s, type) => new List<int>(s.Split(',').Select(e => int.Parse(e))),
		};

		TomlTypeConverter.AddConverter(typeof(List<int>), typeConverter);

		_config = Config;

		_showRecentSkills = _config.Bind("General", "ShowRecentSkills", false, "Shows recently used skills in the playlist");
		_favoriteSkills = _config.Bind("General", "FavoriteSkills", new List<int>(), "Favorite yotogi skills");
		_recentSkills = _config.Bind("General", "RecentSkills", new List<int>(), "Recent yotogi skills");

		_showRecentSkills.SettingChanged += (o, e) => {
			var yotogiManager = YotogiManager.instans;
			if (yotogiManager.play_mgr.maid_ != null) {
				SetSkillArray(yotogiManager);
				yotogiManager.play_mgr.UpdateSkillTower();
			}
		};

		Harmony.CreateAndPatchAll(typeof(FreeYotogiFavorites));
	}

	private static void SetSkillArray(YotogiManager yotogiManager) {
		if (yotogiManager.play_mgr.maid_ == null) {
			return;
		}

		var newPlaySkillArray = new List<Skill.Data> {
			yotogiManager.play_skill_array[0].skill_pair.base_data,
		};

		if (_showRecentSkills.Value) {
			// populate the skill buttons with recent skills
			for (var i = 1; i < _recentSkills.Value.Count; i++) {
				var skillId = _recentSkills.Value[i];
				if (TryGetSkill(yotogiManager.play_mgr.free_skill_select_, skillId, out var skill)) {
					newPlaySkillArray.Add(skill);
				}
			}
		}

		yotogiManager.SetPlaySkillArray(newPlaySkillArray.Select(e => new KeyValuePair<Skill.Data, bool>(e, false)).ToArray());
	}

	private static bool TryGetSkill(FreeSkillSelect freeSkillSelect, int skillId, out Skill.Data skill) {
		foreach (var typeButtonData in freeSkillSelect.button_data_list_) {
			foreach (var categoryButtonData in typeButtonData.children_list) {
				foreach (var skillButtonData in categoryButtonData.children_list) {
					if (skillButtonData.skill_data.id == skillId) {
						skill = skillButtonData.skill_data;
						return true;
					}
				}
			}
		}

		skill = null;
		return false;
	}

	private static bool IsFavoriteSkill(int skillId) {
		return _favoriteSkills.Value.Contains(skillId);
	}

	private static void SetFavoriteSkill(int skillId, bool add) {
		if (add) {
			_favoriteSkills.Value.Add(skillId);
		} else {
			_favoriteSkills.Value.Remove(skillId);
		}
	}

	private static void PushHistorySkill(int skillId) {
		_recentSkills.Value.RemoveAll(e => e == skillId);
		_recentSkills.Value.Insert(0, skillId);
		if (_recentSkills.Value.Count > MaxRecentSkills + 1) {
			_recentSkills.Value.RemoveRange(MaxRecentSkills + 1, _recentSkills.Value.Count - (MaxRecentSkills + 1));
		}
	}

	private static bool TryGetButtonSkillId(UIWFTabButton button, out int skillId) {
		skillId = 0;

		var yotogiManager = YotogiManager.instans;
		if (yotogiManager == null || !yotogiManager.is_free_mode) {
			return false;
		}

		var freeSkillSelect = yotogiManager.free_skill_select_mgr_.free_skill_select_;

		var type = freeSkillSelect.update_obj_dic_[TypeCategoryViewer];
		var category = freeSkillSelect.update_obj_dic_[CategoryViewer];

		// UIWFTabButton objects don't have any reference to its skill data, so we need to check all ButtonData objects in order to find the matching skill button
		var typeButtonData = freeSkillSelect.button_data_list_.Find(e => e.tab_button_obj == type.tab_panel.select_button_);
		if (typeButtonData == null) { return false; }

		var categoryButtonData = typeButtonData.children_list.Find(e => e.tab_button_obj == category.tab_panel.select_button_);
		if (categoryButtonData == null) { return false; }

		var skillButtonData = categoryButtonData.children_list.Find(e => e.tab_button_obj == button);
		if (skillButtonData == null) { return false; }

		skillId = skillButtonData.skill_data.id;
		return true;
	}

	private static void SetUpButton(UIWFTabButton button, FreeSkillSelect.ButtonData buttonData) {
		var skillId = buttonData.skill_data.id;
		var eventDelegate = new EventDelegate(() => {
			if (IsFavoriteSkill(skillId)) {
				SetColor(button, IsFavoriteSkill(skillId));
			}
			// enable click events while the button is selected
			SetBoxCollider(button);
		});
		button.onSelect.Clear();
		button.onSelect.Add(eventDelegate);

		SetColor(button, IsFavoriteSkill(skillId));
		SetBoxCollider(button);
	}

	private static void SetColor(UIWFTabButton button, bool isFavoriteSkill) {
		if (isFavoriteSkill) {
			button.hover = FavoriteSkillColorHover;
			button.defaultColor = button.isSelected ? FavoriteSkillColorHover : FavoriteSkillColor;
		} else {
			button.hover = HoverColor;
			button.defaultColor = button.isSelected ? HoverColor : button.backup_defaultColor;
		}
	}

	private static void SetBoxCollider(UIWFTabButton button) {
		var component = button.gameObject.GetComponent<BoxCollider>();
		if (button.isSelected && component != null && button.widget_ != null) {
			var localSize = button.widget_.localSize;
			component.size = new(localSize.x, localSize.y, 0f);
		}
	}

	[HarmonyPatch(typeof(FreeSkillSelect), nameof(FreeSkillSelect.CreateSkill))]
	[HarmonyPostfix]
	private static void OnCreateSkill(FreeSkillSelect __instance, FreeSkillSelect.ButtonData skill_button_data) {
		// don't color buttons in favorites list
		if (skill_button_data == CategoryButtonData) {
			return;
		}

		var updateObject = __instance.update_obj_dic_[SkillSelectViewer];
		var childList = updateObject.ui_grid.GetChildList();
		for (var i = 0; i < childList.Count; i++) {
			var button = childList[i].gameObject.GetComponentInChildren<UIWFTabButton>();
			if (button != null && button.isEnabled) {
				SetUpButton(button, skill_button_data.children_list[i]);
			}
		}
	}

	[HarmonyPatch(typeof(UIWFTabButton), nameof(UIWFTabButton.OnClick))]
	[HarmonyPrefix]
	private static bool UIWFTabButton_OnClick(UIWFTabButton __instance) {
		if (!(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))) {
			return !__instance.isSelected;
		}

		// set or unset favorites
		if (TryGetButtonSkillId(__instance, out var skillId)) {
			var isFavoriteSkill = !IsFavoriteSkill(skillId);
			SetFavoriteSkill(skillId, isFavoriteSkill);
			_config.Save();

			SetColor(__instance, isFavoriteSkill);
			__instance.PlaySoundClick();

			var freeSkillSelect = YotogiManager.instans.free_skill_select_mgr_.free_skill_select_;
			var categoryButtons = CategoryButtonData.children_list;

			if (!isFavoriteSkill) {
				var buttonDataIndex = categoryButtons.FindIndex(e => e.skill_data.id == skillId);
				var buttonData = categoryButtons[buttonDataIndex];
				categoryButtons.RemoveAt(buttonDataIndex);
				// remove button if currently viewing favorites
				if (TypeButtonData.tab_button_obj.selected_) {
					if (categoryButtons.Count > 0) {
						DestroyImmediate(buttonData.tab_button_obj.transform.parent.gameObject);
						var skill = freeSkillSelect.update_obj_dic_[SkillSelectViewer];
						skill.ui_grid.Reposition();
						skill.tab_panel.UpdateChildren();
						// select next button if removing selected button
						if (skill.tab_panel.select_button_ == null) {
							var item = skill.ui_grid.GetChild(Math.Min(buttonDataIndex, categoryButtons.Count - 1));
							var tabButton = item.gameObject.GetComponentInChildren<UIWFTabButton>();
							skill.tab_panel.Select(tabButton);
						}
					} else {
						// deselect favorites tab if last visible skill was removed
						var type = freeSkillSelect.update_obj_dic_[TypeCategoryViewer];
						foreach (var item in type.ui_grid.GetChildList()) {
							var tabButton = item.gameObject.GetComponentInChildren<UIWFTabButton>();
							if (tabButton.isEnabled && tabButton != TypeButtonData.tab_button_obj) {
								type.tab_panel.Select(tabButton);
								break;
							}
						}
					}
				}
			} else {
				var skillData = Skill.Get(skillId);
				categoryButtons.Add(new() {
					parent = CategoryButtonData,
					name = skillData.name,
					skill_data = skillData,
				});
			}

			TypeButtonData.tab_button_obj.isEnabled = categoryButtons.Count > 0;

			return false;
		}

		return true;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(FreeSkillSelect), nameof(FreeSkillSelect.CreateButtonData))]
	private static void CreateButtonData(List<FreeSkillSelect.ButtonData> __result, Maid maid, HashSet<int> selectableStageIds) {
		CategoryButtonData.children_list.Clear();

		foreach (var data in _favoriteSkills.Value.Select(id => Skill.Get(id))) {
			if (data != null && CanDoYotogiSkill(maid, data) && data.playable_stageid_list.Any(e => selectableStageIds.Contains(e))) {
				CategoryButtonData.children_list.Add(new() {
					parent = CategoryButtonData,
					name = data.name,
					skill_data = data,
				});
			}
		}

		__result.Insert(0, TypeButtonData);
	}

	private static bool CanDoYotogiSkill(Maid maid, Skill.Data data) =>
		data.IsExecMaid(maid.status) &&
		data.IsExecPersonal(maid.status.personal) &&
		data.specialConditionType != Skill.Data.SpecialConditionType.NewType &&
		MaidStatus.PersonalEventBlocker.IsEnabledYotodiSkill(maid.status.personal, data.id);

	[HarmonyPatch(typeof(YotogiPlayManager), nameof(YotogiPlayManager.OnSkillIconClick))]
	[HarmonyPostfix]
	private static void OnSkillIconClick(YotogiPlayManager __instance) {
		if (!__instance.is_free_mode) {
			return;
		}

		var freeSkillSelect = __instance.free_skill_select_;
		var skill = __instance.yotogi_mgr_.play_skill_array[__instance.playing_skill_no_ + 1].skill_pair.base_data;
		freeSkillSelect.select_skill_ = skill;
		freeSkillSelect.SelectSkill(skill, freeSkillSelect.stageExpansionPack);
	}

	[HarmonyPatch(typeof(YotogiPlayManager), nameof(YotogiPlayManager.UpdateSkillTower))]
	[HarmonyPrefix]
	private static void PreUpdateSkillTower(YotogiPlayManager __instance, ref YotogiManager.PlayingSkillData __state) {
		if (!__instance.is_free_mode) {
			return;
		}

		// temporarily set the active skill to the actual active skill, preventing icon from changing when setting favorites
		var playSkillArray = __instance.yotogi_mgr_.play_skill_array;
		__state = playSkillArray[0];
		playSkillArray[0] = _activeSkill;
	}

	[HarmonyPatch(typeof(YotogiPlayManager), nameof(YotogiPlayManager.UpdateSkillTower))]
	[HarmonyPostfix]
	private static void PostUpdateSkillTower(YotogiPlayManager __instance, YotogiManager.PlayingSkillData __state) {
		if (!__instance.is_free_mode) {
			return;
		}

		var playSkillArray = __instance.yotogi_mgr_.play_skill_array;
		playSkillArray[0] = __state;

		// fix buttons getting stuck with mouseover effect after clicking
		var transform = __instance.skill_group_parent_.transform;
		for (var i = 0; i < playSkillArray.Length && i < transform.childCount; i++) {
			if (playSkillArray[i].is_play) {
				var childTransform = transform.GetChild(i);
				var childObject = UTY.GetChildObject(childTransform.gameObject, "Icon");
				var animation = childObject.GetComponent<UIPlayAnimation>();
				animation.Play(false);
			}
		}
	}

	[HarmonyPatch(typeof(YotogiPlayManager), nameof(YotogiPlayManager.NextSkill))]
	[HarmonyPrefix]
	private static void OnNextSkill(YotogiPlayManager __instance) {
		if (!__instance.is_free_mode) {
			return;
		}

		_activeSkill = __instance.yotogi_mgr_.play_skill_array[0];
		PushHistorySkill(_activeSkill.skill_pair.base_data.id);
		_config.Save();
		if (_showRecentSkills.Value) {
			SetSkillArray(__instance.yotogi_mgr_);
		}
	}

	[HarmonyPatch(typeof(FreeSkillSelect), nameof(FreeSkillSelect.OnClickStageEvent))]
	[HarmonyPostfix]
	private static void OnClickStageEvent(FreeSkillSelect __instance) {
		// manually selecting a skill will reset the playlist, so it needs to be set again
		if (_showRecentSkills.Value) {
			SetSkillArray(__instance.yotogi_mgr_);
		}
	}
}
