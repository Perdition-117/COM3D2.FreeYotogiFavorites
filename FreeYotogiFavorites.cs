using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using Yotogis;

namespace COM3D2.FreeYotogiFavorites;

[BepInPlugin("net.perdition.com3d2.freeyotogifavorites", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
class FreeYotogiFavorites : BaseUnityPlugin {
	private const int MaxSkills = 6;

	private static readonly Color HoverColor = Color.white;
	private static readonly Color FavoriteSkillColor = new(218 / 255f, 218 / 255f, 187 / 255f);
	private static readonly Color FavoriteSkillColorHover = new(255 / 255f, 255 / 255f, 224 / 255f);

	private static ConfigFile _config;
	private static ConfigEntry<bool> _isHistoryMode;
	private static ConfigEntry<List<int>> _favoriteSkills;
	private static ConfigEntry<List<int>> _recentSkills;

	private static YotogiManager.PlayingSkillData _activeSkill;

	void Awake() {
		var typeConverter = new TypeConverter {
			ConvertToString = (o, type) => string.Join(",", ((List<int>)o).Select(e => e.ToString()).ToArray()),
			ConvertToObject = (s, type) => new List<int>(s.Split(',').Select(e => int.Parse(e))),
		};

		TomlTypeConverter.AddConverter(typeof(List<int>), typeConverter);

		_config = Config;

		_isHistoryMode = _config.Bind("General", "HistoryMode", false, "Enables history mode, showing recently used skills instead of favorites");
		_favoriteSkills = _config.Bind("General", "FavoriteSkills", new List<int>(), "Favorite yotogi skills");
		_recentSkills = _config.Bind("General", "RecentSkills", new List<int>(), "Recent yotogi skills");

		Harmony.CreateAndPatchAll(typeof(FreeYotogiFavorites));
	}

	private static void SetSkillArray(YotogiManager yotogiManager) {
		if (yotogiManager.play_mgr.maid_ == null) {
			return;
		}

		// populate the skill buttons with our favorite skills
		var pinnedSkills = _isHistoryMode.Value ? _recentSkills.Value.GetRange(1, _recentSkills.Value.Count - 1) : _favoriteSkills.Value;
		var newPlaySkillArray = new List<Skill.Data> {
			yotogiManager.play_skill_array[0].skill_pair.base_data,
		};
		foreach (var skillId in pinnedSkills) {
			if (TryGetSkill(yotogiManager.play_mgr.free_skill_select_, skillId, out var skill)) {
				newPlaySkillArray.Add(skill);
			}
		}
		yotogiManager.SetPlaySkillArray(newPlaySkillArray.Select(e => new KeyValuePair<Skill.Data, bool>(e, false)).ToArray());

		// disable the currently active skill's button
		var playSkillArray = yotogiManager.play_skill_array;
		for (var i = 0; i < playSkillArray.Length; i++) {
			playSkillArray[i].is_play = playSkillArray[i].skill_pair.base_data == _activeSkill?.skill_pair.base_data;
		}
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
		if (_recentSkills.Value.Count > MaxSkills + 1) {
			_recentSkills.Value.RemoveRange(MaxSkills + 1, _recentSkills.Value.Count - (MaxSkills + 1));
		}
	}

	private static bool TryGetButtonSkillId(UIWFTabButton button, out int skillId) {
		skillId = 0;

		if (!YotogiManager.instans?.is_free_mode ?? false) {
			return false;
		}

		var freeSkillSelect = YotogiManager.instans.free_skill_select_mgr_.free_skill_select_;

		var type = freeSkillSelect.update_obj_dic_["タイプ"];
		var category = freeSkillSelect.update_obj_dic_["カテゴリー"];

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

	[HarmonyPatch(typeof(FreeSkillSelect), "CreateSkill")]
	[HarmonyPostfix]
	private static void OnCreateSkill(FreeSkillSelect __instance, FreeSkillSelect.ButtonData skill_button_data) {
		if (_isHistoryMode.Value) {
			return;
		}

		var updateObject = __instance.update_obj_dic_["スキル"];
		var childList = updateObject.ui_grid.GetChildList();
		for (var i = 0; i < childList.Count; i++) {
			var button = childList[i].gameObject.GetComponentInChildren<UIWFTabButton>();
			if (button != null && button.isEnabled) {
				SetUpButton(button, skill_button_data.children_list[i]);
			}
		}
	}

	[HarmonyPatch(typeof(UIWFTabButton), "OnClick")]
	[HarmonyPrefix]
	private static bool UIWFTabButton_OnClick(UIWFTabButton __instance) {
		if (_isHistoryMode.Value || !(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))) {
			return !__instance.isSelected;
		}

		// set or unset favorites
		if (TryGetButtonSkillId(__instance, out var skillId)) {
			var isFavoriteSkill = IsFavoriteSkill(skillId);
			if (isFavoriteSkill || _favoriteSkills.Value.Count < MaxSkills) {
				SetFavoriteSkill(skillId, !isFavoriteSkill);
				_config.Save();

				SetColor(__instance, !isFavoriteSkill);
				__instance.PlaySoundClick();

				var yotogiManager = YotogiManager.instans;
				if (yotogiManager.play_mgr.maid_ != null) {
					SetSkillArray(yotogiManager);
					yotogiManager.play_mgr.UpdateSkillTower();
				}
			}
			return false;
		}

		return true;
	}

	[HarmonyPatch(typeof(YotogiPlayManager), "OnSkillIconClick")]
	[HarmonyPostfix]
	private static void OnSkillIconClick(YotogiPlayManager __instance) {
		if (!__instance.is_free_mode) {
			return;
		}

		var freeSkillSelect = __instance.free_skill_select_;
		var skill = __instance.yotogi_mgr_.play_skill_array[__instance.playing_skill_no_ + 1].skill_pair.base_data;
		freeSkillSelect.select_skill_ = skill;
		freeSkillSelect.SelectSkill(skill, freeSkillSelect.user_request_stage);
	}

	[HarmonyPatch(typeof(YotogiPlayManager), "UpdateSkillTower")]
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

	[HarmonyPatch(typeof(YotogiPlayManager), "UpdateSkillTower")]
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

	[HarmonyPatch(typeof(YotogiPlayManager), "NextSkill")]
	[HarmonyPrefix]
	private static void OnNextSkill(YotogiPlayManager __instance) {
		if (!__instance.is_free_mode) {
			return;
		}

		_activeSkill = __instance.yotogi_mgr_.play_skill_array[0];
		PushHistorySkill(_activeSkill.skill_pair.base_data.id);
		_config.Save();
		SetSkillArray(__instance.yotogi_mgr_);
	}

	[HarmonyPatch(typeof(FreeSkillSelect), "OnClickStageEvent")]
	[HarmonyPostfix]
	private static void OnClickStageEvent(FreeSkillSelect __instance) {
		// manually selecting a skill will reset the playlist, so it needs to be set again
		SetSkillArray(__instance.yotogi_mgr_);
	}
}
