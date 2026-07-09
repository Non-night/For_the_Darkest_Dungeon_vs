using System.Collections.Generic;

namespace For_the_Darkest_Dungeon.DefinitionDarkest
{
    public static class DarkestEffectsData
    {
        // 核心关键字
        public static readonly HashSet<string> CoreKeywords = new HashSet<string>
        {
            ".name", ".target", ".on_hit", ".on_miss"
        };

        // 反击关键字
        public static readonly HashSet<string> RiposteKeywords = new HashSet<string>
        {
            ".riposte", ".riposte_on_miss_chance_add", ".riposte_on_hit_chance_add", ".riposte_on_miss_chance_multiply", ".riposte_on_hit_chance_multiply", ".riposte_effect"
        };

        // Buff 关键字
        public static readonly HashSet<string> BuffKeywords = new HashSet<string>
        {
            ".combat_stat_buff", ".buff_ids", ".buff_amount", ".buff_type", ".buff_sub_type",
            ".buff_duration_type", ".buff_source_type", ".buff_is_clear_debuff_valid",
            ".damage_low_multiply", ".damage_low_add", ".damage_high_multiply", ".damage_high_add",
            ".max_hp_multiply", ".max_hp_add", ".attack_rating_add", ".attack_rating_multiply", ".crit_chance_add",
            ".crit_chance_multiply", ".defense_rating_add", ".defense_rating_multiply", ".protection_rating_add",
            ".protection_rating_multiply", ".speed_rating_add", ".speed_rating_multiply", ".guard"
        };

        // Summon 关键字
        public static readonly HashSet<string> SummonKeywords = new HashSet<string>
        {
            ".summon_monsters", ".summon_chances", ".summon_ranks", ".summon_limits", ".summon_count",
            ".summon_erase_data_on_roll", ".summon_can_spawn_loot", ".summon_rank_is_previous_monster_class",
            ".summon_does_roll_initiatives"
        };

        // 所有的关键字（用于自动补全）
        public static readonly List<string> AllKeywords = new List<string>
        {
            ".name", ".target", ".curio_result_type", ".chance", ".on_hit", ".on_miss", ".queue",
            ".dotBleed", ".dotPoison", ".dotStress", ".dotHpHeal", ".healstress", ".stress",
            ".combat_stat_buff", ".damage_low_multiply", ".damage_low_add", ".damage_high_multiply", ".damage_high_add",
            ".max_hp_multiply", ".max_hp_add", ".attack_rating_add", ".attack_rating_multiply", ".crit_chance_add",
            ".crit_chance_multiply", ".defense_rating_add", ".defense_rating_multiply", ".protection_rating_add",
            ".protection_rating_multiply", ".speed_rating_add", ".speed_rating_multiply", ".buff_ids",
            ".duration", ".heal", ".heal_percent", ".can_crit_heal", ".cure", ".cure_bleed", ".cure_poison",
            ".clearDotStress", ".tag", ".untag", ".stun", ".unstun", ".keyStatus", ".riposte",
            ".riposte_on_miss_chance_add", ".riposte_on_hit_chance_add",
            ".riposte_on_miss_chance_multiply", ".riposte_on_hit_chance_multiply", ".riposte_effect",
            ".clear_riposte", ".guard", ".clearguarding", ".clearguarded", ".torch_decrease",
            ".torch_increase", ".item", ".curio", ".dotShuffle", ".push", ".pull", ".shuffletarget",
            ".shuffleparty", ".instant_shuffle", ".buff_amount", ".buff_type", ".buff_sub_type",
            ".buff_duration_type", ".steal_buff_stat_type", ".steal_buff_source_type",
            ".swap_source_and_target", ".kill", ".immobilize", ".unimmobilize", ".control",
            ".uncontrol", ".kill_enemy_types", ".monsterType", ".capture", ".capture_remove_from_party",
            ".disease", ".remove_vampire", ".summon_monsters", ".summon_chances", ".summon_ranks",
            ".summon_limits", ".summon_count", ".summon_erase_data_on_roll", ".summon_can_spawn_loot",
            ".summon_rank_is_previous_monster_class", ".summon_does_roll_initiatives",
            ".crit_doesnt_apply_to_roll", ".virtue_blockable_chance", ".affliction_blockable_chance",
            ".set_mode", ".can_apply_on_death", ".apply_once", ".rank_target", ".clear_rank_target",
            ".performer_rank_target", ".apply_with_result", ".initiative_change", ".source_heal_type",
            ".skill_instant", ".actor_dot", ".health_damage", ".bark", ".set_monster_class_id",
            ".set_monster_class_ids", ".set_monster_class_chances", ".set_monster_class_reset_hp",
            ".set_monster_class_reset_buffs", ".set_monster_class_carry_over_hp_min_percent",
            ".set_monster_class_clear_initative", ".set_monster_class_clear_monster_brain_cooldowns",
            ".set_monster_class_reset_scale", ".has_description", ".stealth", ".unstealth",
            ".clear_debuff", ".health_damage_blocks", ".dotSource", ".buff_source_type",
            ".use_item_id", ".use_item_type", ".skips_endless_wave_curio",
            ".spawn_target_actor_base_class_id", ".clearvirtue", ".riposte_validate",
            ".buff_is_clear_debuff_valid", ".refreshes_skill_uses", ".cure_disease",
            ".individual_target_actor_rolls", ".damage_type", ".damage_source_type",
            ".damage_source_data", ".daze", ".undaze"
        };

        // 参数表
        // target 参数
        public static readonly List<string> TargetValues = new List<string>
        {
            "performer", "performer_group", "performer_group_other", "target", "target_group", "target_group_other", "target_enemy_group", "global"
        };
        // curio_result_type 参数
        public static readonly List<string> CurioResultValues = new List<string>
        {
            "positive", "negative", "neutral", "none"
        };
        // keyStatus 参数
        public static readonly List<string> KeyStatusValues = new List<string>
        {
            "tagged", "poisoned", "bleeding", "stunned", "dazed"
        };
        // Buff Type 参数
        public static readonly List<string> BuffTypeValues = new List<string>
        {
            "hp_heal_amount", "hp_heal_percent", "hp_heal_received_percent", "combat_stat_multiply",
           "combat_stat_add", "resistance", "poison_chance", "bleed_chance", "stress_dmg_percent",
           "stress_dmg_received_percent", "stress_heal_percent", "stress_heal_received_percent",
            "party_surprise_chance", "monsters_surprise_chance", "ambush_chance", "scouting_chance",
            "starving_damage_percent", "upgrade_discount", "damage_received_percent", "debuff_chance",
            "resolve_check_percent", "stun_chance", "move_chance", "remove_negative_quirk_chance",
            "food_consumption_percent", "resolve_xp_bonus_percent", "activity_side_effect_chance",
            "vampire_evolution_duration", "quirk_evolution_death_immune", "disable_combat_skill_attribute",
            "guard_blocked", "tag_blocked", "ignore_protection", "ignore_stealth", "crit_received_chance",
            "riposte", "status", "tag", "guarded", "vampire", "stealth", "hp_dot_bleed", "hp_dot_poison",
            "hp_dot_heal", "stress_dot", "shuffle_dot", "torch_increase_percent", "torch_decrease_percent",
            "torchlight_burn_percent", "stress_on_miss", "stress_from_idle_in_town", "shard_reward_percent",
            "shard_consume_percent", "damage_reflect_percent", "hp_dot_bleed_duration_received_percent",
            "hp_dot_bleed_duration_percent", "hp_dot_bleed_amount_received_percent", "hp_dot_bleed_amount_percent",
            "hp_dot_poison_duration_received_percent", "hp_dot_poison_duration_percent",
            "hp_dot_poison_amount_received_percent", "hp_dot_poison_amount_percent",
            "stress_dot_duration_received_percent", "stress_dot_duration_percent",
            "stress_dot_amount_received_percent", "stress_dot_amount_percent",
            "hp_heal_dot_duration_received_percent", "hp_heal_dot_duration_percent",
            "hp_heal_dot_amount_received_percent", "hp_heal_dot_amount_percent",
            "shuffle_dot_duration_received_percent", "shuffle_dot_duration_percent",
            "guard_duration_received_percent", "guard_duration_percent", "cure_bleed_received_chance",
            "cure_poison_received_chance", "cure_bleed_chance", "cure_poison_chance",
            "random_target_friendly_chance", "random_target_attack_chance",
            "transfer_debuff_from_attacker_chance", "transfer_buff_from_attacker_chance",
            "quirk_tag_evolution_duration", "deathblow_chance", "heartattack_stress_heal_percent",
            "ignore_guard", "buff_duration_percent", "riposte_duration_percent"
        };
        // Buff Sub Type 参数
        public static readonly List<string> BuffSubTypeValues = new List<string>
        {
            "max_hp", "damage_low", "damage_high", "attack_rating", "crit_chance",
            "defense_rating", "protection_rating", "speed_rating", "riposte_on_hit_chance",
            "riposte_on_miss_chance", "stun", "move", "poison", "bleed", "disease",
            "debuff", "death_blow", "trap", "armour", "weapon", "combat_skill",
            "camping_skill", "add_currency", "remove_currency", "add_trinket",
            "remove_trinket", "activity_lock", "apply_buff", "go_missing", "heal",
            "buff", "tag", "stress", "guard", "daze", "hero_skill",
            "hero_skill_multi_target", "monster_skill", "monster_skill_multi_target",
            "camp_skill", "camp_skill_multi_target", "companion", "eat", "act_out",
            "damage_heal", "effect", "flashback", "dot", "hunger", "hero_crit",
            "hero_killing_blow", "mode", "control", "unkown", "town_idle",
            "quest_fail", "pass", "camping_relieve_stress", "camping_eat", "tile",
            "retreat", "capture", "monster_crit"
        };
        // Buff Duration Type 参数
        public static readonly List<string> BuffDurationTypeValues = new List<string>
        {
            "round", "combat_end", "quest_end", "quest_complete", "quest_not_complete", "activity_end",
        "idle_start_town_visit", "till_removed", "none", "before_turn", "after_turn", "after_round"
        };
        // Buff Source 参数
        public static readonly List<string> BuffSourceValues = new List<string>
        {
            "bsrc_skill", "bsrc_notspecified", "bsrc_affliction", "bsrc_virtue",
            "bsrc_item", "bsrc_curio", "bsrc_disease", "bsrc_riposte",
            "bsrc_campingskill", "bsrc_quirk", "bsrc_trinket", "bsrc_trinket_set",
            "bsrc_instantSkill", "bsrc_guard", "bsrc_deathsdoor",
            "bsrc_deathsdoor_recovery", "bsrc_deathsdoor_recovery_heart_attack",
            "bsrc_quest_failure", "bsrc_companion", "bsrc_stun", "bsrc_town",
            "bsrc_district", "bsrc_torchsettings", "bsrc_crit",
            "bsrc_trinket_additional_effect", "bsrc_battle_modifier",
            "bsrc_never_again", "bsrc_vampire", "bsrc_town_event",
            "bsrc_flashback_start", "bsrc_flashback_result",
            "bsrc_completed_darkest_dungeon_quest_party_hero",
            "bsrc_quest_modifier", "bsrc_last_hero", "combat_end"
        };
        // Heal Source 参数
        public static readonly List<string> HealSourceValues = new List<string>
        {
            "hero_skill", "hero_skill_multi_target",
            "monster_skill", "monster_skill_multi_target",
            "camp_skill", "camp_skill_multi_target",
            "companion", "eat", "act_out", "damage_heal",
            "effect", "flashback", "dot", "curio"
        };
        // Damage Type 参数
        public static readonly List<string> DamageTypeValues = new List<string>
        {
            "unknown", "trap", "obstacle", "hunger", "attack", "bleed", "healing",
            "poisoned", "captor", "ddexit", "townexit", "death", "heartattack",
            "theblood", "effect", "quirkevolutiondeath", "reflect", "riposte",
            "additionaleffect", "supply", "quest_item", "trinket",  "estate_currency",
            "journal_page", "torch", "shovel"
        };
        // Damage Source 参数
        public static readonly List<string> DamageSourceValues = new List<string>
        {
            "unknown", "hunger", "trap", "obstacle", "friendly",
            "monster", "hero", "friendly_quirk_actout", "friendly_trait_actout",
            "item", "effect", "quirk", "reflect", "trinket", "estate"
        };
        // 数字布尔参数
        public static readonly List<string> NumBoolValues = new List<string>
        {
            "0", "1"
        };
        // 字符布尔参数
        public static readonly List<string> StrBoolValues = new List<string>
        {
            "false", "true"
        };
        // 字符布尔参数-报错检验用
        public static readonly List<string> StrBoolValuesForError = new List<string>
        {
            "false", "true", "False", "True", "FALSE", "TRUE"
        };

        // 映射：关键字 -> 对应的补全列表
        public static readonly Dictionary<string, List<string>> KeywordToValuesMap = new Dictionary<string, List<string>>
        {
            // 独立映射
            { ".target", TargetValues },
            { ".curio_result_type", CurioResultValues },
            { ".keyStatus", KeyStatusValues },
            
            // buff type
            { ".buff_type", BuffTypeValues },
            { ".steal_buff_stat_type", BuffTypeValues },

            // buff sub type
            { ".buff_sub_type", BuffSubTypeValues },

            // buff duration type
            { ".buff_duration_type", BuffDurationTypeValues },

            // buff source
            { ".buff_source_type", BuffSourceValues },
            { ".steal_buff_source_type", BuffSourceValues },
            { ".dotSource", BuffSourceValues },

            // heal source
            { ".source_heal_type", HealSourceValues },

            // damage type
            { ".damage_type", DamageTypeValues },

            // damage source
            { ".damage_source_type", DamageSourceValues },

            // 数字布尔类
            { ".combat_stat_buff", NumBoolValues },
            { ".cure", NumBoolValues },
            { ".cure_bleed", NumBoolValues },
            { ".cure_poison", NumBoolValues },
            { ".clearDotStress", NumBoolValues },
            { ".tag", NumBoolValues },
            { ".untag", NumBoolValues },
            { ".unstun", NumBoolValues },
            { ".riposte", NumBoolValues },
            { ".clear_riposte", NumBoolValues },
            { ".guard", NumBoolValues },
            { ".clearguarding", NumBoolValues },
            { ".clearguarded", NumBoolValues },
            { ".item", NumBoolValues },
            { ".curio", NumBoolValues },
            { ".dotShuffle", NumBoolValues },
            { ".kill", NumBoolValues },
            { ".immobilize", NumBoolValues },
            { ".unimmobilize", NumBoolValues },
            { ".uncontrol", NumBoolValues },
            { ".capture", NumBoolValues },
            { ".capture_remove_from_party", NumBoolValues },
            { ".remove_vampire", NumBoolValues },
            { ".summon_does_roll_initiatives", NumBoolValues },
            { ".performer_rank_target", NumBoolValues },
            { ".stealth", NumBoolValues },
            { ".unstealth", NumBoolValues },
            { ".clear_debuff", NumBoolValues },
            { ".clearvirtue", NumBoolValues },
            { ".cure_disease", NumBoolValues },
            { ".daze", NumBoolValues },
            { ".undaze", NumBoolValues },

            // 字符布尔类
            { ".on_hit", StrBoolValues },
            { ".on_miss", StrBoolValues },
            { ".queue", StrBoolValues },
            { ".can_crit_heal", StrBoolValues },
            { ".swap_source_and_target", StrBoolValues },
            { ".crit_doesnt_apply_to_roll", StrBoolValues },
            { ".can_apply_on_death", StrBoolValues },
            { ".apply_once", StrBoolValues },
            { ".apply_with_result", StrBoolValues },
            { ".skill_instant", StrBoolValues },
            { ".set_monster_class_reset_buffs", StrBoolValues },
            { ".set_monster_class_clear_initative", StrBoolValues },
            { ".set_monster_class_clear_monster_brain_cooldowns", StrBoolValues },
            { ".set_monster_class_reset_scale", StrBoolValues },
            { ".has_description", StrBoolValues },
            { ".skips_endless_wave_curio", StrBoolValues },
            { ".riposte_validate", StrBoolValues },
            { ".buff_is_clear_debuff_valid", StrBoolValues },
            { ".refreshes_skill_uses", StrBoolValues },
            { ".individual_target_actor_rolls", StrBoolValues },
            { ".summon_can_spawn_loot", StrBoolValues },
            { ".set_monster_class_reset_hp", StrBoolValues },
            { ".summon_erase_data_on_roll", StrBoolValues },
            { ".summon_rank_is_previous_monster_class", StrBoolValues }
        };

        // 双布尔类关键字（用于报错检验）
        public static readonly HashSet<string> DoubleBoolKeywords = new HashSet<string>
        {
			".set_monster_class_reset_hp"
        };

        // 双布尔参数-报错检验用
        public static readonly List<string> DoubleBoolValuesForError = new List<string>
        {
            "0", "1", "false", "true", "False", "True", "FALSE", "TRUE"
        };

        // 简写buff相关
        // 主副类型对应
        public static readonly Dictionary<string, HashSet<string>> BuffTypeToSubTypesMap = new Dictionary<string, HashSet<string>>
        {
            { "hp_heal_amount", new HashSet<string> { "hero_skill", "hero_skill_multi_target", "monster_skill", "monster_skill_multi_target", "camp_skill", "camp_skill_multi_target", "companion", "eat", "act_out", "damage_heal", "effect", "flashback", "dot", "curio" } },
            { "hp_heal_percent", new HashSet<string> { "hero_skill", "hero_skill_multi_target", "monster_skill", "monster_skill_multi_target", "camp_skill", "camp_skill_multi_target", "companion", "eat", "act_out", "damage_heal", "effect", "flashback", "dot", "curio" } },
            { "hp_heal_received_percent", new HashSet<string> { "hero_skill", "hero_skill_multi_target", "monster_skill", "monster_skill_multi_target", "camp_skill", "camp_skill_multi_target", "companion", "eat", "act_out", "damage_heal", "effect", "flashback", "dot", "curio" } },
            { "combat_stat_multiply", new HashSet<string> { "max_hp", "damage_low", "damage_high", "attack_rating", "crit_chance", "defense_rating", "protection_rating", "speed_rating" } },
            { "combat_stat_add", new HashSet<string> { "max_hp", "damage_low", "damage_high", "attack_rating", "crit_chance", "defense_rating", "protection_rating", "speed_rating" } },
            { "resistance", new HashSet<string> { "stun", "move", "poison", "bleed", "disease", "debuff", "death_blow", "trap" } },
            { "stress_dmg_percent", new HashSet<string> { "hunger", "death_blow", "hero_crit", "hero_killing_blow", "mode", "control", "unkown", "town_idle", "quest_fail", "pass", "camping_relieve_stress", "camping_eat", "tile", "retreat", "effect", "capture", "monster_crit" } },
            { "stress_dmg_received_percent", new HashSet<string> { "hunger", "death_blow", "hero_crit", "hero_killing_blow", "mode", "control", "unkown", "town_idle", "quest_fail", "pass", "camping_relieve_stress", "camping_eat", "tile", "retreat", "effect", "capture", "monster_crit" } },
            { "stress_heal_percent", new HashSet<string> { "hunger", "death_blow", "hero_crit", "hero_killing_blow", "mode", "control", "unkown", "town_idle", "quest_fail", "pass", "camping_relieve_stress", "camping_eat", "tile", "retreat", "effect", "capture", "monster_crit" } },
            { "stress_heal_received_percent", new HashSet<string> { "hunger", "death_blow", "hero_crit", "hero_killing_blow", "mode", "control", "unkown", "town_idle", "quest_fail", "pass", "camping_relieve_stress", "camping_eat", "tile", "retreat", "effect", "capture", "monster_crit" } },
            { "activity_side_effect_chance", new HashSet<string> { "add_currency", "remove_currency", "add_trinket", "remove_trinket", "activity_lock", "apply_buff", "go_missing" } },
            { "disable_combat_skill_attribute", new HashSet<string> { "heal", "buff", "debuff", "bleed", "poison", "stun", "tag", "stress", "move", "guard", "daze" } }
        };
		// 必须写副类型的主类型
        public static readonly HashSet<string> MustHaveSubBuffTypes = new HashSet<string>
        {
            "combat_stat_multiply", "combat_stat_add", "resistance", "activity_side_effect_chance", "disable_combat_skill_attribute"
        };
		// 副类型可以自由填写的主类型
		public static readonly HashSet<string> SubFreeBuffTypes = new HashSet<string>
        {
			"upgrade_discount", "riposte", "quirk_tag_evolution_duration"
		};

	}

	public static class DarkestInfoData
    {
        // 1. 所有行首关键字列表 (Header)
        public static readonly List<string> AllHeaders = new List<string>
        {
            "display_modifier:",
            "riposte_skill:",
            "rendering:",
            "controlled:",
            "health_bar:",
            "mode:",
            "hp_reaction:",
            "death_reaction:",
            "crit:",
            "additional_effect:",
            "display:",
            "commonfx:",
            "battle_backdrop:",
            "wave_background:",
            "stats:",
            "skill:",
            "personality:",
            "loot:",
            "tag:",
            "enemy_type:",
            "defending_area_pos_offset:",
            "initiative:",
            "monster_brain:",
            "captor_empty:",
            "captor_full:",
            "life_link:",
            "shared_health:",
            "shape_shifter:",
            "torchlight_modifier:",
            "battle_modifier:",
            "death_class:",
            "death_damage:",
            "life_time:",
            "controller:",
            "battle_stage:",
            "companion:",
            "skill_reaction:",
            "audio_modifier:",
            "spawn:",
            "mash_modifier:",
            "torch_settings:",
            "kill_quirk:",
            "tutorial:",
            "wave_spawning:",
            "colour_grade:",
            "resistances:",
            "weapon:",
            "armour:",
            "combat_skill:",
            "combat_move_skill:",
            "id_index:",
            "sorting_index:",
            "generation:",
            "skill_selection:",
            "incompatible_party_member:",
            "deaths_door:",
            "last_hero:",
            "extra_battle_loot:",
            "extra_curio_loot:",
            "extra_shard_bonus:",
            "extra_stack_limit:",
            "progression:",
            "overstressed_modifier:",
            "activity_modifier:",
            "quirk_modifier:",
            "act_out_display:",
            "restriction:"
        };

        // 2. 行首关键字与其对应的次级关键字映射 (.keyword)
        public static readonly Dictionary<string, List<string>> InfoContextMap = new Dictionary<string, List<string>>
        {
            { "display_modifier:", new List<string> { ".disable_halos", ".disabled_popup_text_types", ".use_centre_skill_announcement", ".disable_health", ".anim_override", ".show_spawn_fx" } },
            { "riposte_skill:", new List<string> {
                ".id",
                ".dmg",
                ".atk",
                ".def",
                ".move",
                ".crit",
                ".level",
                ".type",
                ".starting_cooldown",
                ".per_battle_limit",
                ".per_turn_limit",
                ".is_continue_turn",
                ".launch",
                ".target",
                ".self_target_valid",
                ".extra_targets_chance",
                ".extra_targets_count",
                ".is_crit_valid",
                ".effect",
                ".valid_modes",
                ".ignore_stealth",
                ".ignore_guard",
                ".can_miss",
                ".can_be_riposted",
                ".ignore_protection",
                ".required_performer_hp_range",
                ".rank_damage_modifiers",
                ".heal",
                ".can_crit_heal",
                ".generation_guaranteed",
                ".is_user_selected_targets",
                ".is_knowledgeable",
                ".is_monster_rerank_valid_on_attack",
                ".is_monster_rerank_valid_on_friendly_presentation_end",
                ".is_monster_rerank_valid_on_friendly_post_result",
                ".is_stall_invalidating",
                ".refresh_after_each_wave",
                ".damage_heal_base_class_ids",
                ".ignore_deathsdoor",
                ".icon",
                ".anim",
                ".fx",
                ".targfx",
                ".targheadfx",
                ".targchestfx",
                ".misstargfx",
                ".misstargheadfx",
                ".misstargchestfx",
                ".area_pos_offset",
                ".target_area_pos_offset",
                ".reset_source_stance",
                ".reset_target_stance",
                ".can_display_selection",
                ".hide_performer_health",
                ".condensed_tooltip_effects",
                ".condensed_tooltip_stats",
                ".condensed_tooltip_type",
                ".condensed_tooltip_effects_per_line",
                ".nil",
                ".custom_target_anim",
                ".has_crit_vo",
                ".custom_idle_anim_name",
                ".custom_idle_round_duration",
                ".can_display_skill_name",
                ".can_display_performer_selection_after_turn"
            } },
            { "rendering:", new List<string> { ".sort_position_z_rank_override" } },
            { "controlled:", new List<string> { ".target_rank" } },
            { "health_bar:", new List<string> { ".type" } },
            { "mode:", new List<string> { ".id", ".is_raid_default", ".bark_override_id", ".stress_damage_per_turn", ".battle_complete_combat_skill_id", ".affliction_combat_skill_id", ".always_guard_actor_base_class_ids", ".is_targetable", ".keep_rounds_in_ranks" } },
            { "hp_reaction:", new List<string> { ".hp_ratio", ".is_under", ".effects" } },
            { "death_reaction:", new List<string> { ".target_allies", ".target_enemies", ".effects" } },
            { "crit:", new List<string> { ".effects", ".is_valid_effects_target" } },
            { "additional_effect:", new List<string> { ".is_valid_trinket_target", ".is_valid_trinket_attacker" } },
            { "display:", new List<string> { ".size" } },
            { "commonfx:", new List<string> { ".deathfx" } },
            { "battle_backdrop:", new List<string> { ".background_name", ".animation", ".isFlat" } },
            { "wave_background:", new List<string> { ".background_name", ".animation" } },
            { "stats:", new List<string> { ".hp", ".prot", ".def", ".spd", ".stun_resist", ".move_resist", ".poison_resist", ".bleed_resist", ".disease_resist", ".debuff_resist", ".death_blow_resist", ".trap_resist" } },
            { "skill:", new List<string> {
                ".id",
                ".dmg",
                ".atk",
                ".def",
                ".move",
                ".crit",
                ".level",
                ".type",
                ".starting_cooldown",
                ".per_battle_limit",
                ".per_turn_limit",
                ".is_continue_turn",
                ".launch",
                ".target",
                ".self_target_valid",
                ".extra_targets_chance",
                ".extra_targets_count",
                ".is_crit_valid",
                ".effect",
                ".valid_modes",
                ".ignore_stealth",
                ".ignore_guard",
                ".can_miss",
                ".can_be_riposted",
                ".ignore_protection",
                ".required_performer_hp_range",
                ".rank_damage_modifiers",
                ".heal",
                ".can_crit_heal",
                ".generation_guaranteed",
                ".is_user_selected_targets",
                ".is_knowledgeable",
                ".is_monster_rerank_valid_on_attack",
                ".is_monster_rerank_valid_on_friendly_presentation_end",
                ".is_monster_rerank_valid_on_friendly_post_result",
                ".is_stall_invalidating",
                ".refresh_after_each_wave",
                ".damage_heal_base_class_ids",
                ".ignore_deathsdoor",
                ".icon",
                ".anim",
                ".fx",
                ".targfx",
                ".targheadfx",
                ".targchestfx",
                ".misstargfx",
                ".misstargheadfx",
                ".misstargchestfx",
                ".area_pos_offset",
                ".target_area_pos_offset",
                ".reset_source_stance",
                ".reset_target_stance",
                ".can_display_selection",
                ".hide_performer_health",
                ".condensed_tooltip_effects",
                ".condensed_tooltip_stats",
                ".condensed_tooltip_type",
                ".condensed_tooltip_effects_per_line",
                ".nil",
                ".custom_target_anim",
                ".has_crit_vo",
                ".custom_idle_anim_name",
                ".custom_idle_round_duration",
                ".can_display_skill_name",
                ".can_display_performer_selection_after_turn"
            } },
            { "death_damage:", new List<string> { ".target_base_class_id", ".target_damage" } },
            { "personality:", new List<string> { ".prefskill" } },
            { "loot:", new List<string> { ".code", ".count", ".raid_finish_quirk_class_id" } },
            { "tag:", new List<string> { ".id" } },
            { "enemy_type:", new List<string> { ".id" } },
            { "defending_area_pos_offset:", new List<string> { ".offset" } },
            { "initiative:", new List<string> { ".number_of_turns_per_round", ".hide_indicator" } },
            { "monster_brain:", new List<string> { ".id" } },
            { "captor_empty:", new List<string> { ".performing_monster_captor_base_class", ".captor_full_monster_class", ".capture_effects", ".reset_hp", ".count_captor_full_damage" } },
            { "captor_full:", new List<string> {
                ".captor_empty_monster_class", ".release_on_death", ".release_on_prisoner_at_deaths_door",
                ".release_on_prisoner_affliction", ".switch_class_on_death", ".release_effects",
                ".per_turn_damage_percent", ".per_turn_stress_damage", ".has_prisoner_overlay",
                ".unique_first_action_sfx", ".reset_hp", ".use_previous_monster_class_hp",
                ".add_current_hp", ".use_bark_offset"
            } },
            { "life_link:", new List<string> { ".base_class", ".class", ".does_spawn_loot", ".is_death_class_valid" } },
            { "shared_health:", new List<string> { ".id" } },
            { "shape_shifter:", new List<string> { ".monster_class_ids", ".monster_class_chances", ".monster_class_valid_ranks", ".round_frequency", ".fx_name" } },
            { "torchlight_modifier:", new List<string> { ".min", ".max" } },
            { "battle_modifier:", new List<string> {
                ".disable_stall_penalty", ".does_count_towards_stall_penalty", ".accelerate_stall_penalty",
                ".can_surprise", ".can_be_surprised", ".always_surprise", ".always_be_surprised",
                ".can_relieve_stress_from_crit", ".can_relieve_stress_from_killing_blow", ".can_be_summon_rank",
                ".does_count_as_monster_size_for_monster_brain", ".does_count_as_guardable_for_monster_brain",
                ".can_be_missed", ".can_be_hit", ".is_valid_friendly_target", ".can_be_damaged_directly",
                ".can_be_random_target", ".can_be_guarded", ".remove_on_retreat", ".living_other_enemy_buffs",
                ".living_hero_buff_instance_ids", ".disabled_act_out_combat_start_turn_types"
            } },
            { "death_class:", new List<string> {
                ".monster_class_id", ".random_monster_class_ids", ".random_monster_class_chances",
                ".use_previous_monster_hp", ".is_valid_on_bleed_dot", ".is_valid_on_blight_dot",
                ".is_valid_on_crit", ".reset_scale_anim", ".on_change_sfx", ".type", ".can_die_from_damage",
                ".carry_over_hp_min_percent", ".clear_monster_brain_cooldowns", ".change_class_effects"
            } },
            { "life_time:", new List<string> { ".alive_round_limit", ".does_check_for_loot" } },
            { "controller:", new List<string> { ".stress_per_controlled_turn", ".uncontrol_effects" } },
            { "battle_stage:", new List<string> { ".id" } },
            { "companion:", new List<string> { ".monster_class", ".heal_per_turn_percent", ".buffs" } },
            { "skill_reaction:", new List<string> {
                ".was_hit_performer_effects", ".was_hit_target_effects", ".was_killed_other_monsters_effects",
                ".was_killed_by_hero_effects", ".was_killed_all_heroes_effects", ".was_killed_effects"
            } },
            { "audio_modifier:", new List<string> { ".intensity", ".variation_count", ".ambience_parameter_ids", ".ambience_parameter_values" } },
            { "spawn:", new List<string> { ".effects", ".wave_effects" } },
            { "mash_modifier:", new List<string> { ".disable_additional_mash_for_infestation_sequence_on_death" } },
            { "torch_settings:", new List<string> { ".torch_settings_id" } },
            { "kill_quirk:", new List<string> { } },
            { "tutorial:", new List<string> { ".id" } },
            { "wave_spawning:", new List<string> { ".prefers_front" } },
            { "colour_grade:", new List<string> { ".name" } },
            { "resistances:", new List<string> { ".stun", ".move", ".poison", ".bleed", ".disease", ".debuff", ".death_blow", ".trap" } },
            { "weapon:", new List<string> { ".name", ".atk", ".dmg", ".crit", ".spd", ".icon", ".upgradeRequirementCode" } },
            { "armour:", new List<string> { ".name", ".def", ".prot", ".hp", ".spd",  ".icon", ".upgradeRequirementCode" } },
            { "combat_skill:", new List<string> {
                ".id",
                ".dmg",
                ".atk",
                ".def",
                ".move",
                ".crit",
                ".level",
                ".type",
                ".starting_cooldown",
                ".per_battle_limit",
                ".per_turn_limit",
                ".is_continue_turn",
                ".launch",
                ".target",
                ".self_target_valid",
                ".extra_targets_chance",
                ".extra_targets_count",
                ".is_crit_valid",
                ".effect",
                ".valid_modes",
                ".ignore_stealth",
                ".ignore_guard",
                ".can_miss",
                ".can_be_riposted",
                ".ignore_protection",
                ".required_performer_hp_range",
                ".rank_damage_modifiers",
                ".heal",
                ".can_crit_heal",
                ".generation_guaranteed",
                ".is_user_selected_targets",
                ".is_knowledgeable",
                ".is_monster_rerank_valid_on_attack",
                ".is_monster_rerank_valid_on_friendly_presentation_end",
                ".is_monster_rerank_valid_on_friendly_post_result",
                ".is_stall_invalidating",
                ".refresh_after_each_wave",
                ".damage_heal_base_class_ids",
                ".ignore_deathsdoor",
                ".icon",
                ".anim",
                ".fx",
                ".targfx",
                ".targheadfx",
                ".targchestfx",
                ".misstargfx",
                ".misstargheadfx",
                ".misstargchestfx",
                ".area_pos_offset",
                ".target_area_pos_offset",
                ".reset_source_stance",
                ".reset_target_stance",
                ".can_display_selection",
                ".hide_performer_health",
                ".condensed_tooltip_effects",
                ".condensed_tooltip_stats",
                ".condensed_tooltip_type",
                ".condensed_tooltip_effects_per_line",
                ".nil",
                ".custom_target_anim",
                ".has_crit_vo",
                ".custom_idle_anim_name",
                ".custom_idle_round_duration",
                ".can_display_skill_name",
                ".can_display_performer_selection_after_turn"
            } },
            { "combat_move_skill:", new List<string> {
                ".id",
                ".dmg",
                ".atk",
                ".def",
                ".move",
                ".crit",
                ".level",
                ".type",
                ".starting_cooldown",
                ".per_battle_limit",
                ".per_turn_limit",
                ".is_continue_turn",
                ".launch",
                ".target",
                ".self_target_valid",
                ".extra_targets_chance",
                ".extra_targets_count",
                ".is_crit_valid",
                ".effect",
                ".valid_modes",
                ".ignore_stealth",
                ".ignore_guard",
                ".can_miss",
                ".can_be_riposted",
                ".ignore_protection",
                ".required_performer_hp_range",
                ".rank_damage_modifiers",
                ".heal",
                ".can_crit_heal",
                ".generation_guaranteed",
                ".is_user_selected_targets",
                ".is_knowledgeable",
                ".is_monster_rerank_valid_on_attack",
                ".is_monster_rerank_valid_on_friendly_presentation_end",
                ".is_monster_rerank_valid_on_friendly_post_result",
                ".is_stall_invalidating",
                ".refresh_after_each_wave",
                ".damage_heal_base_class_ids",
                ".ignore_deathsdoor",
                ".icon",
                ".anim",
                ".fx",
                ".targfx",
                ".targheadfx",
                ".targchestfx",
                ".misstargfx",
                ".misstargheadfx",
                ".misstargchestfx",
                ".area_pos_offset",
                ".target_area_pos_offset",
                ".reset_source_stance",
                ".reset_target_stance",
                ".can_display_selection",
                ".hide_performer_health",
                ".condensed_tooltip_effects",
                ".condensed_tooltip_stats",
                ".condensed_tooltip_type",
                ".condensed_tooltip_effects_per_line",
                ".nil",
                ".custom_target_anim",
                ".has_crit_vo",
                ".custom_idle_anim_name",
                ".custom_idle_round_duration",
                ".can_display_skill_name",
                ".can_display_performer_selection_after_turn"
            } },
            { "id_index:", new List<string> { ".index"} },
            { "sorting_index:", new List<string> { ".index"} },
            { "generation:", new List<string> {
                ".is_generation_enabled", ".number_of_positive_quirks_min", ".number_of_positive_quirks_max",
                ".number_of_negative_quirks_min", ".number_of_negative_quirks_max", ".number_of_class_specific_camping_skills",
                ".number_of_shared_camping_skills", ".number_of_random_combat_skills", ".number_of_cards_in_deck",
                ".card_chance", ".reduce_number_of_cards_in_deck_hero_class_id", ".reduce_number_of_cards_in_deck_amount", ".town_event_dependency" } },
            { "skill_selection:", new List<string> { ".can_select_combat_skills", ".number_of_selected_combat_skills_max" } },
            { "incompatible_party_member:", new List<string> { ".id", ".hero_tag" } },
            { "deaths_door:", new List<string> { ".buffs", ".recovery_buffs", ".recovery_heart_attack_buffs", ".enter_effects", ".enter_effect_round_cooldown" } },
            { "last_hero:", new List<string> { ".buffs" } },
            { "extra_battle_loot:", new List<string> { ".code", ".count" } },
            { "extra_curio_loot:", new List<string> { ".code", ".count" } },
            { "extra_shard_bonus:", new List<string> { ".amount" } },
            { "extra_stack_limit:", new List<string> { ".id" } },
            { "progression:", new List<string> { ".has_caretaker_goals" } },
            { "overstressed_modifier:", new List<string> { ".override_trait_type_ids", ".override_trait_type_chances" } },
            { "activity_modifier:", new List<string> { ".override_valid_activity_ids", ".override_stress_removal_amount_low", ".override_stress_removal_amount_high" } },
            { "quirk_modifier:", new List<string> { ".incompatible_class_ids" } },
            { "act_out_display:", new List<string> { ".attack_friendly_anim", ".attack_friendly_fx", ".attack_friendly_targchestfx", ".attack_friendly_sfx" } },
            { "restriction:", new List<string> { ".enabled_dlc" } },
        };

        // 3. 关键字与其对应的可选参数值映射 (Value)
        public static readonly Dictionary<string, List<string>> KeywordValueMap = new Dictionary<string, List<string>>
        {
            // 布尔值列表 (通用)
            { "BOOL", new List<string> { "true", "false" } },

            // disabled_act_out_combat_start_turn_types
            { ".disabled_act_out_combat_start_turn_types", new List<string>
                {
                    "nothing", "bark_stress", "change_pos", "ignore_command", "random_command",
                    "retreat_from_combat", "attack_friendly", "attack_self", "mark_self", "stress_heal_self",
                    "stress_heal_party", "buff_random_party_member", "buff_party", "heal_self", "consume_item"
                }
            },

            // 技能type
            { "SKILL_TYPE", new List<string> { "melee", "ranged", "move", "teleport" } },

            // 表格中列出的 54 个弹出文本类型
            { ".disabled_popup_text_types", new List<string>
                {
                    "actor_dot_complete", "pass", "hp_heal_dot_onset", "hp_heal_dot", "hp_heal_dot_crit",
                    "miss", "no_damage", "crit_damage", "damage", "death_avoided", "deathblow",
                    "hero_heal", "hero_heal_crit", "monster_heal", "monster_heal_crit", "stress_reduce",
                    "stress_damage", "resist", "move_resist", "disease_resist", "buff", "debuff",
                    "debuff_resist", "stun", "stun_resist", "stun_clear", "poison", "poison_resist",
                    "bleed", "bleed_resist", "cured", "cure_failed", "tagged", "guard", "guard_failed",
                    "riposte", "full", "heart_attack", "heal_failed", "vampire", "vampire_resist",
                    "stress_dot", "stress_dot_resist", "shuffle_dot", "shuffle_dot_resist",
                    "health_damage_block_onset", "health_damage_block", "tag_block", "damage_reflect",
                    "control_resist", "refresh_skills", "daze", "daze_resist", "guard_break"
                }
            }
        };

        // 获取特定 Keyword 对应的 Value 补全列表的辅助方法
        public static List<string> GetValuesForKeyword(string header, string keyword)
        {
            // 1. 优先检查预定义的特殊 Value 列表
            if (KeywordValueMap.ContainsKey(keyword)) return KeywordValueMap[keyword];

            // 2. 检查特殊情况
            if (header == "riposte_skill:" && keyword == ".type") return KeywordValueMap["SKILL_TYPE"];
            if (header == "skill:" && keyword == ".type") return KeywordValueMap["SKILL_TYPE"];
            if (header == "combat_skill:" && keyword == ".type") return KeywordValueMap["SKILL_TYPE"];
            if (header == "combat_move_skill:" && keyword == ".type") return KeywordValueMap["SKILL_TYPE"];

            // 3. 检查是否是布尔类型的关键字 (对应表格中标记为 true/false 的项)
            if (IsBooleanKeyword(keyword)) return KeywordValueMap["BOOL"];

            return null;
        }

        public static bool IsKeywordHasStaticValues(string keyword)
        {
            return KeywordValueMap.ContainsKey(keyword) || IsBooleanKeyword(keyword) || keyword == ".type";
        }

        private static bool IsBooleanKeyword(string keyword)
        {
            // 汇总表格中明确标注有 true/false 选项的所有关键字
            var boolKeys = new HashSet<string> {
                ".disable_halos", ".use_centre_skill_announcement", ".disable_health",
                ".show_spawn_fx", ".can_crit_heal", ".is_continue_turn", ".is_crit_valid",
                ".reset_source_stance", ".reset_target_stance", ".hide_performer_health",
                ".is_monster_rerank_valid_on_attack", ".is_monster_rerank_valid_on_friendly_presentation_end",
                ".is_monster_rerank_valid_on_friendly_post_result", ".can_display_performer_selection_after_turn",
                ".can_display_skill_name", ".can_display_selection", ".can_miss",
                ".can_be_riposted", ".ignore_protection", ".ignore_guard", ".ignore_stealth",
                ".ignore_deathsdoor", ".has_crit_vo", ".is_stall_invalidating",
                ".refresh_after_each_wave", ".is_raid_default", ".is_targetable",
                ".keep_rounds_in_ranks", ".is_under", ".target_allies", ".target_enemies",
                ".is_valid_effects_target", ".is_valid_trinket_target", ".isFlat",
                ".is_user_selected_targets", ".is_knowledgeable", ".hide_indicator",
                ".count_captor_full_damage", ".release_on_death", ".release_on_prisoner_at_deaths_door",
                ".release_on_prisoner_affliction", ".switch_class_on_death", ".has_prisoner_overlay",
                ".unique_first_action_sfx", ".reset_hp", ".use_previous_monster_class_hp",
                ".add_current_hp", ".use_bark_offset", ".does_spawn_loot", ".is_death_class_valid",
                ".disable_stall_penalty", ".does_count_towards_stall_penalty", ".accelerate_stall_penalty",
                ".can_surprise", ".can_be_surprised", ".always_surprise", ".always_be_surprised",
                ".can_relieve_stress_from_crit", ".can_relieve_stress_from_killing_blow",
                ".can_be_summon_rank", ".does_count_as_monster_size_for_monster_brain",
                ".does_count_as_guardable_for_monster_brain", ".can_be_missed",
                ".can_be_hit", ".is_valid_friendly_target", ".can_be_damaged_directly",
                ".can_be_random_target", ".can_be_guarded", ".remove_on_retreat",
                ".use_previous_monster_hp", ".is_valid_on_bleed_dot", ".is_valid_on_blight_dot",
                ".is_valid_on_crit", ".reset_scale_anim", ".on_change_sfx",
                ".can_die_from_damage", ".clear_monster_brain_cooldowns", ".does_check_for_loot",
                ".disable_additional_mash_for_infestation_sequence_on_death", ".prefers_front",
                ".nil", ".generation_guaranteed", ".condensed_tooltip_type",
                ".condensed_tooltip_stats", ".condensed_tooltip_effects", ".is_generation_enabled",
                ".can_select_combat_skills", ".has_caretaker_goals", ".self_target_valid",
            };
            return boolKeys.Contains(keyword);
        }

		// 针对info中的参数长度相关数据
        // 单字符串长度32
		public static readonly HashSet<(string Header, string Keyword)> SingleString32 = new HashSet<(string Header, string Keyword)>
		{
            ( "display_modifier:", ".anim_override" ), ( "mode:", ".id" ), ( "mode:", ".bark_override_id" ),
            ( "shape_shifter:", ".monster_class_ids" ), ( "death_class:", ".type" ), ( "incompatible_party_member:", ".id" ),
            ( "extra_battle_loot:", ".code" ), ( "extra_curio_loot:", ".code" ), ( "restriction:", ".enabled_dlc" )
		};
        // 单字符串长度64
        public static readonly HashSet<(string Header, string Keyword)> SingleString64 = new HashSet<(string Header, string Keyword)>
        {
            // 反击
            ("riposte_skill:", ".id"), ("riposte_skill:", ".type"), ("riposte_skill:", ".anim"), ("riposte_skill:", ".fx"), ("riposte_skill:", ".targfx"),
            ("riposte_skill:", ".targheadfx"), ("riposte_skill:", ".targchestfx"), ("riposte_skill:", ".misstargfx"), ("riposte_skill:", ".misstargheadfx"),
            ("riposte_skill:", ".misstargchestfx"), ("riposte_skill:", ".custom_target_anim"),
            
            ("health_bar:", ".type"), ("commonfx:", ".deathfx"), ("commonfx:", ".id"),

			("battle_backdrop:", ".background_name"), ("battle_backdrop:", ".animation"),
            
            ("wave_background:", ".background_name"), ("wave_background:", ".animation"),
            // 怪物技能
            ("skill:", ".id"), ("skill:", ".type"), ("skill:", ".anim"), ("skill:", ".fx"), ("skill:", ".targfx"), ("skill:", ".targheadfx"),
            ("skill:", ".targchestfx"), ("skill:", ".misstargfx"), ("skill:", ".misstargheadfx"), ("skill:", ".misstargchestfx"), ("skill:", ".custom_target_anim"),
            // loot
			("loot:", ".code"), ("loot:", ".raid_finish_quirk_class_id"),

            ("tag:", ".id"),

            ("enemy_type:", ".id"),
            
            ("monster_brain:", ".id"),
            // 容器
            ("captor_empty:", ".performing_monster_captor_base_class"), ("captor_empty:", ".captor_full_monster_class"), ("captor_full:", ".captor_empty_monster_class"),
            // 生命链接
            ("life_link:", ".base_class"), ("life_link:", ".class"),

            ("shared_health:", ".id"),
            
            ("death_class:", ".monster_class_id"),
            
            ("death_damage:", ".target_base_class_id"),
            
            ("battle_stage:", ".id"),
            
            ("companion:", ".monster_class"),
            
            ("torch_settings:", ".torch_settings_id"),
            
            ("tutorial:", ".id"),
            
            ("colour_grade:", ".name"),
            // 刀甲
            ("weapon:", ".name"), ("weapon:", ".icon"), ("armour:", ".name"), ("armour:", ".icon"),
            // 英雄技能
            ("combat_skill:", ".id"), ("combat_skill:", ".icon"), ("combat_skill:", ".type"), ("combat_skill:", ".anim"), ("combat_skill:", ".fx"),
            ("combat_skill:", ".targfx"), ("combat_skill:", ".targheadfx"), ("combat_skill:", ".targchestfx"), ("combat_skill:", ".misstargfx"),
            ("combat_skill:", ".misstargheadfx"), ("combat_skill:", ".misstargchestfx"), ("combat_skill:", ".custom_target_anim"),
            // 移动技能
            ("combat_move_skill:", ".id"), ("combat_move_skill:", ".icon"), ("combat_move_skill:", ".type"), ("combat_move_skill:", ".anim"),
            ("combat_move_skill:", ".fx"), ("combat_move_skill:", ".targfx"), ("combat_move_skill:", ".targheadfx"), ("combat_move_skill:", ".targchestfx"),
            ("combat_move_skill:", ".misstargfx"), ("combat_move_skill:", ".misstargheadfx"), ("combat_move_skill:", ".misstargchestfx"),
            ("combat_move_skill:", ".custom_target_anim"),
            // 马车刷新
            ("generation:", ".reduce_number_of_cards_in_deck_hero_class_id"), ("generation:", ".town_event_dependency"),
            
            ("incompatible_party_member:", ".hero_tag"),
            
            ("extra_stack_limit:", ".id"),
            
            ("overstressed_modifier:", ".id"),
            // act_out
            ("act_out_display:", ".attack_friendly_anim"), ("act_out_display:", ".attack_friendly_fx"), ("act_out_display:", ".attack_friendly_targchestfx"),
            ("act_out_display:", ".attack_friendly_sfx")
        };
        // 单字符串长度128
        public static readonly HashSet<(string Header, string Keyword)> SingleString128 = new HashSet<(string Header, string Keyword)>
        {
            ("shape_shifter:", ".fx_name")
        };
        // 单字符串长度512
        public static readonly HashSet<(string Header, string Keyword)> SingleString512 = new HashSet<(string Header, string Keyword)>
        {
            ("mode:", ".battle_complete_combat_skill_id"), ("mode:", ".affliction_combat_skill_id")
        };

        // 多参数长度限制
        public static readonly Dictionary<(string Header, string Keyword), (int MaxArgs, int MaxLength)> MultiStringLengthRules = new Dictionary<(string Header, string Keyword), (int MaxArgs, int MaxLength)>
        {
            // 技能类
            {("skill:", ".effect"), (8, 64) }, {("skill:", ".valid_modes"), (4, 64)}, {("skill:", ".damage_heal_base_class_ids"), (4, 64)},
            {("riposte_skill:", ".effect"), (8, 64) }, {("riposte_skill:", ".valid_modes"), (4, 64)}, {("riposte_skill:", ".damage_heal_base_class_ids"), (4, 64)},
            {("combat_skill:", ".effect"), (8, 64) }, {("combat_skill:", ".valid_modes"), (4, 64)}, {("combat_skill:", ".damage_heal_base_class_ids"), (4, 64)},
            {("combat_move_skill:", ".effect"), (8, 64) }, {("combat_move_skill:", ".valid_modes"), (4, 64)}, {("combat_move_skill:", ".damage_heal_base_class_ids"), (4, 64)},
            // 模式
            {("mode:", ".always_guard_actor_base_class_ids"), (4, 64) },
            // 生命反馈
            {("hp_reaction:", ".effects"), (4, 64) },
            // 死亡反馈
            {("death_reaction:", ".effects"), (4, 64) },
            // 暴击特效
            {("crit:", ".effects"), (4, 64) },
            // 容器
            {("captor_empty:", ".capture_effects"), (4, 64) }, {("captor_full:", ".release_effects"), (4, 64)},
            // battle_modifier
            {("battle_modifier:", ".living_other_enemy_buffs"), (8, 64) }, {("battle_modifier:", ".living_hero_buff_instance_ids"), (8, 64)},
            //死亡变身
            {("death_class:", ".random_monster_class_ids"), (4, 64) }, {("death_class:", ".change_class_effects"), (4, 64) },
            // controller
            {("controller:", ".uncontrol_effects"), (4, 64) },
            // companion
            {("companion:", ".buffs"), (8, 64) },
            // 受击反馈
            {("skill_reaction:", ".was_hit_performer_effects"), (4, 64) }, {("skill_reaction:", ".was_hit_target_effects"), (4, 64) },
            {("skill_reaction:", ".was_killed_other_monsters_effects"), (4, 64) }, {("skill_reaction:", ".was_killed_by_hero_effects"), (4, 64) },
            {("skill_reaction:", ".was_killed_all_heroes_effects"), (4, 64) }, {("skill_reaction:", ".was_killed_effects"), (4, 64) },
            // audio_modifier
            {("audio_modifier:", ".ambience_parameter_ids"), (4, 64) },
            // spawn
            {("spawn:", ".effects"), (4, 31) }, {("spawn:", ".wave_effects"), (4, 64) },
            // 死门
            {("deaths_door:", ".buffs"), (8, 64) }, {("deaths_door:", ".recovery_buffs"), (8, 64) },
            {("deaths_door:", ".recovery_heart_attack_buffs"), (8, 64) }, {("deaths_door:", ".enter_effects"), (8, 64) },
            // last_hero
            {("last_hero:", ".buffs"), (8, 64) },
            // 爆压调节器
            {("overstressed_modifier:", ".override_trait_type_ids"), (8, 64) },
            // activity_modifier
            {("activity_modifier:", ".override_valid_activity_ids"), (10, 64) },
            // 怪癖调节器
            {("quirk_modifier:", ".incompatible_class_ids"), (10, 64) },
            // shape_shifter
            {("shape_shifter:", ".monster_class_ids"), (4, 32) }
        };

        // 参数个数限制
        public static readonly Dictionary<(string Header, string Keyword), int> MaxArgumentCountRules = new Dictionary<(string Header, string Keyword), int>()
        {
            // 技能类
            {("skill:", ".heal"), 2}, {("skill:", ".move"), 2}, {("skill:", ".rank_damage_modifiers"), 4}, {("skill:", ".area_pos_offset"), 2},
            {("skill:", ".target_area_pos_offset"), 2}, {("skill:", ".required_performer_hp_range"), 2}, 
            {("riposte_skill:", ".heal"), 2}, {("riposte_skill:", ".move"), 2}, {("riposte_skill:", ".rank_damage_modifiers"), 4}, {("riposte_skill:", ".area_pos_offset"), 2},
            {("riposte_skill:", ".target_area_pos_offset"), 2}, {("riposte_skill:", ".required_performer_hp_range"), 2}, 
            {("combat_skill:", ".heal"), 2}, {("combat_skill:", ".move"), 2}, {("combat_skill:", ".rank_damage_modifiers"), 4}, {("combat_skill:", ".area_pos_offset"), 2},
            {("combat_skill:", ".target_area_pos_offset"), 2}, {("combat_skill:", ".required_performer_hp_range"), 2}, 
            {("combat_move_skill:", ".heal"), 2}, {("combat_move_skill:", ".move"), 2}, {("combat_move_skill:", ".rank_damage_modifiers"), 4}, {("combat_move_skill:", ".area_pos_offset"), 2},
            {("combat_move_skill:", ".target_area_pos_offset"), 2}, {("combat_move_skill:", ".required_performer_hp_range"), 2},
            // defending_area_pos_offset
            {("defending_area_pos_offset:", ".offset"), 2},
            // shape_shifter
            {("shape_shifter:", ".monster_class_chances"), 4},
            // 随机死亡变身
            {("death_class:", ".random_monster_class_chances"), 4},
            // audio_modifier
            {("audio_modifier:", ".ambience_parameter_values"), 4},
            // 武器
            {("weapon:", ".dmg"), 2},
            // 爆压调节器
            {("overstressed_modifier:", ".override_trait_type_chances"), 8}
        };
	}
}