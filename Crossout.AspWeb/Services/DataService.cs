﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Crossout.Model;
using Crossout.Model.Items;
using Crossout.Model.Recipes;
using Crossout.AspWeb.Models;
using Crossout.AspWeb.Models.EditRecipe;
using Crossout.AspWeb.Models.General;
using Crossout.AspWeb.Models.Items;
using Crossout.AspWeb.Models.Recipes;
using ZicoreConnector.Zicore.Connector.Base;
using Crossout.Data.PremiumPackages;
using Crossout.AspWeb.Models.Changes;
using Crossout.AspWeb.Models.Language;
using Crossout.AspWeb.Models.Info;
using Crossout.AspWeb.Models.Drafts.BadgeExchange;
using Crossout.AspWeb.Models.Drafts.Snipe;
using Crossout.AspWeb.Pocos;
using NPoco;
using System.Data.Common;
using Crossout.AspWeb.Models.Cod;

namespace Crossout.AspWeb.Services
{
    public class DataService
    {
        protected SqlConnector DB { get; set; }
        protected IDatabase NPoco { get; set; }

        public DataService(SqlConnector sql)
        {
            DB = sql;
            NPoco = new Database(sql.CreateConnection());
        }

        public ItemModel SelectItem(int id, bool addData, int language)
        {
            ItemModel itemModel = new ItemModel();
            var parmeter = new List<Parameter>();
            parmeter.Add(new Parameter { Identifier = "id", Value = id });
            parmeter.Add(new Parameter { Identifier = "language", Value = language });

            string query = BuildSearchQuery(false, false, false, true, false, false, false, true, true, false);

            var ds = DB.SelectDataSet(query, parmeter);

            NPoco.Connection.Open();
            itemModel.OCRStatItems = NPoco.Fetch<OCRStatItemPoco>("WHERE itemnumber = @0", id);
            itemModel.OCRStatItems.ForEach(x => x.CreateDisplayStats());
            itemModel.OCRStatItems = itemModel.OCRStatItems.GroupBy(x => x.XoVer).Select(x => x.OrderByDescending(x => x.Timestamp).First()).ToList();
            itemModel.OCRStatItems.Sort(OCRStatItemPoco.CompareDateTimeDesc);
            NPoco.Connection.Close();

            var item = Item.Create(ds[0]);
            if (addData)
            {
                CrossoutDataService.Instance.AddData(item);
            }
            itemModel.Item = item;
            return itemModel;
        }

        public ItemSynergyCollection SelectItemSynergy(int id, int language)
        {
            ItemSynergyCollection itemSynergies = new ItemSynergyCollection();

            var parmeter = new List<Parameter>();
            parmeter.Add(new Parameter { Identifier = "id", Value = id });

            itemSynergies.ItemNumber = id;

            NPoco.Connection.Open();
            itemSynergies.Synergies = NPoco.Fetch<SynergyPoco>("WHERE itemnumber = @0", id);
            var synergyTypes = itemSynergies.Synergies.Select(x => x.SynergyType).ToList();
            var synergyItems = NPoco.Fetch<SynergyItem>(@"SELECT itemsynergies.*, item.*, itemlocalization.*, rarity.* FROM itemsynergies 
                                                        LEFT JOIN itemlocalization ON itemlocalization.itemnumber = itemsynergies.itemnumber 
                                                        LEFT JOIN item ON item.id = itemsynergies.itemnumber 
                                                        LEFT JOIN rarity ON rarity.id = item.raritynumber
                                                        WHERE itemsynergies.synergy IN (@0) AND itemsynergies.itemnumber <> @1 AND itemlocalization.languagenumber = @2 ORDER BY rarity.order", synergyTypes, id, language);
            itemSynergies.SynergyItems.AddRange(synergyItems);
            NPoco.Connection.Close();

            return itemSynergies;
        }

        public Dictionary<int, Item> SelectListOfItems(List<int> ids, int language)
        {
            Dictionary<int, Item> items = new Dictionary<int, Item>();
            string query = BuildItemsQueryFromIDList(ids);
            List<Parameter> parameters = new List<Parameter>();
            parameters.Add(new Parameter { Identifier = "language", Value = language });
            var ds = DB.SelectDataSet(query, parameters);

            foreach (var row in ds)
            {
                Item item = new Item();
                int i = 0;
                item.Id = row[i++].ConvertTo<int>();
                item.Name = row[i++].ConvertTo<string>();
                item.SellPrice = row[i++].ConvertTo<int>();
                item.BuyPrice = row[i++].ConvertTo<int>();
                item.Amount = row[i++].ConvertTo<int>();
                item.LocalizedName = row[i].ConvertTo<string>();
                items.Add(item.Id, item);
            }

            return items;
        }

        public RecipeModel SelectRecipeModel(Item item, bool resolveDeep, int language, bool addWorkbenchItem = true)
        {
            RecipeModel recipeModel = new RecipeModel();
            RecipeCounter counter = new RecipeCounter();
            recipeModel.Recipe = new RecipeItem(counter) { Item = item, Ingredients = SelectRecipe(counter, item, language) };

            ResolveRecipe(counter, recipeModel.Recipe, 1, resolveDeep, addWorkbenchItem, language);

            CalculateRecipe(recipeModel.Recipe);
            recipeModel.Recipe.IngredientSum = CreateIngredientItem(counter, recipeModel.Recipe);

            return recipeModel;
        }

        public void ResolveRecipe(RecipeCounter counter, RecipeItem parent, int depth, bool resolveDeep, bool addWorkbenchItem, int language)
        {
            foreach (var ingredient in parent.Ingredients)
            {
                ingredient.Parent = parent;
                ingredient.Depth = depth;
                if (ingredient.Item.RecipeId > 0 && resolveDeep)
                {
                    ingredient.Ingredients = SelectRecipe(counter, ingredient.Item, language);
                    ++depth;
                    ResolveRecipe(counter, ingredient, depth, true, addWorkbenchItem, language);
                    CalculateRecipe(ingredient);
                    if (ingredient.Depth > 0)
                    {
                        ingredient.IngredientSum = CreateIngredientItem(counter, ingredient);
                    }
                    parent.MaxDepth = Math.Max(depth, ingredient.MaxDepth);
                    depth--;
                }
            }

            if (addWorkbenchItem)
            {
                AddWorkbenchCostItem(counter, parent, depth);
            }
        }

        public WorkbenchItemId GetWorkbenchItemIdByRarity(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Common_1:
                    return WorkbenchItemId.Common_445;
                case Rarity.Rare_2:
                    return WorkbenchItemId.Rare_446;
                case Rarity.Epic_3:
                    return WorkbenchItemId.Epic_447;
                case Rarity.Legendary_4:
                    return WorkbenchItemId.Legendary_448;
                case Rarity.Relic_5:
                    return WorkbenchItemId.Relic_449;
                case Rarity.Special_6:
                    return WorkbenchItemId.Special_466;
                default: return WorkbenchItemId.Common_445;
            }
        }

        public void AddWorkbenchCostItem(RecipeCounter counter, RecipeItem parent, int depth)
        {
            if (parent.Ingredients.Count > 0)
            {
                var rarity = (Rarity)parent.Item.RarityId;
                // We don't want common (no workbench costs) to be displayed. 
                // Also incase the workbench costs are not based on the rarity we use the override value from the DB
                if (parent.Item.WorkbenchRarity > 0)
                {
                    rarity = (Rarity)parent.Item.WorkbenchRarity;
                }

                if (rarity != Rarity.Common_1)
                {
                    var id = GetWorkbenchItemIdByRarity(rarity);
                    var workbenchItem = SelectItem((int)id, false, 1);
                    parent.Ingredients.Add(CreateIngredientWorkbenchItem(counter, parent, workbenchItem, depth));
                }
            }
        }

        private static RecipeItem CreateIngredientItem(RecipeCounter counter, RecipeItem item)
        {
            var ingredientSum = new RecipeItem(counter)
            {
                Id = -1,
                Depth = item.Depth,
                Item = new Item
                {
                    Id = item.Item.Id,
                    RecipeId = item.Item.RecipeId,

                    Name = item.Item.Name,
                    SellPrice = item.SumSell,
                    BuyPrice = item.SumBuy,
                    SellOffers = item.Item.SellOffers,
                    BuyOrders = item.Item.BuyOrders,
                    RarityId = item.Item.RarityId,
                    RarityName = item.Item.RarityName,
                    CategoryId = item.Item.CategoryId,
                    CategoryName = item.Item.CategoryName,
                    TypeId = item.Item.TypeId,
                    TypeName = item.Item.TypeName,
                    CraftingResultAmount = item.Item.CraftingResultAmount,
                    LocalizedName = item.Item.LocalizedName
                }
            };
            ingredientSum.Parent = item;
            ingredientSum.IsSumRow = true;
            return ingredientSum;
        }

        private static RecipeItem CreateIngredientWorkbenchItem(RecipeCounter counter, RecipeItem parent, ItemModel item, int depth)
        {
            var ingredient = new RecipeItem(counter)
            {
                Id = -1,
                Depth = depth,
                Item = new Item
                {
                    Id = item.Item.Id,
                    RecipeId = item.Item.RecipeId,

                    Name = item.Item.Name,
                    SellPrice = item.Item.SellPrice,
                    BuyPrice = item.Item.BuyPrice,
                    SellOffers = item.Item.SellOffers,
                    BuyOrders = item.Item.BuyOrders,
                    RarityId = item.Item.RarityId,
                    RarityName = item.Item.RarityName,
                    CategoryId = item.Item.CategoryId,
                    CategoryName = item.Item.CategoryName,
                    TypeId = item.Item.TypeId,
                    TypeName = item.Item.TypeName
                }
            };
            ingredient.Number = 1;
            ingredient.Parent = parent;
            return ingredient;
        }

        private static void CalculateRecipe(RecipeItem item)
        {
            item.SumBuy = item.Ingredients.Sum(x => x.BuyPriceTimesNumber) / Math.Max(item.Item.CraftingResultAmount, 1);
            item.SumSell = item.Ingredients.Sum(x => x.SellPriceTimesNumber) / Math.Max(item.Item.CraftingResultAmount, 1);
        }

        public List<RecipeItem> SelectRecipe(RecipeCounter counter, Item item, int language)
        {
            var parmeter = new List<Parameter>();
            parmeter.Add(new Parameter { Identifier = "id", Value = item.RecipeId });
            parmeter.Add(new Parameter { Identifier = "language", Value = language });
            string query = BuildRecipeQuery();
            var ds = DB.SelectDataSet(query, parmeter);
            return RecipeItem.Create(counter, new RecipeItem(counter) { Item = item }, ds);
        }

        public IngredientUsageModel SelectIngredientUsage(int itemId)
        {
            var parmeter = new List<Parameter>();
            parmeter.Add(new Parameter { Identifier = "itemnumber", Value = itemId });
            string query = BuildIngredientUsageQuery();
            var ds = DB.SelectDataSet(query, parmeter);
            var ingredientUsageModel = new IngredientUsageModel();
            foreach (var row in ds)
            {
                if (row.All(x => x != null))
                {
                    int i = 0;
                    var item = new IngredientUsageItem
                    {
                        RecipeId = row[i++].ConvertTo<int>(),
                        ItemId = row[i++].ConvertTo<int>(),
                        Amount = row[i++].ConvertTo<int>()
                    };
                    if (item.ItemId != 0)
                    {
                        ingredientUsageModel.IngredientUsageList.Add(item);
                    }
                }
            }
            return ingredientUsageModel;
        }

        public StatusModel SelectStatus()
        {
            var ds = DB.SelectDataSet(BuildStatusQuery());
            var model = new StatusModel
            {
                Id = Convert.ToInt32(ds[0][0]),
                LastUpdate = Convert.ToDateTime(ds[0][1])
            };

            return model;
        }

        public List<Item> SelectAllActiveItems(int language, bool excludeRemovedItems = true)
        {
            string query = BuildAllActiveItemsQuery(excludeRemovedItems);
            List<Parameter> parameters = new List<Parameter>();
            parameters.Add(new Parameter { Identifier = "language", Value = language });
            var ds = DB.SelectDataSet(query, parameters);
            return Item.CreateAllItemsForEdit(ds);
        }

        public List<FactionModel> SelectAllFactions()
        {
            return CreateAllFactionsForEdit(DB.SelectDataSet(BuildFactionsQuery()));
        }

        public Dictionary<int, string> SelectAllRarities()
        {
            var dict = new Dictionary<int, string>();
            var ds = DB.SelectDataSet(BuildRarityQuery());
            foreach (var row in ds)
            {
                dict.Add(row[0].ConvertTo<int>(), row[1].ConvertTo<string>());
            }
            return dict;
        }

        public Dictionary<int, string> SelectAllCategories()
        {
            var dict = new Dictionary<int, string>();
            var ds = DB.SelectDataSet(BuildCategoryQuery());
            foreach (var row in ds)
            {
                dict.Add(row[0].ConvertTo<int>(), row[1].ConvertTo<string>());
            }
            return dict;
        }

        public Dictionary<int, string> SelectAllTypes()
        {
            var dict = new Dictionary<int, string>();
            var ds = DB.SelectDataSet(BuildTypeQuery());
            foreach (var row in ds)
            {
                dict.Add(row[0].ConvertTo<int>(), row[1].ConvertTo<string>());
            }
            return dict;
        }

        public List<AppPrices> SelectAllSteamPrices()
        {
            List<AppPrices> appPrices = new List<AppPrices>();
            var ds = DB.SelectDataSet(BuildSteamPricesQuery());
            foreach (var row in ds)
            {
                List<Currency> currencys = new List<Currency>();
                currencys.Add(new Currency() { Final = row[1].ConvertTo<int>(), CurrencyAbbriviation = "USD" });
                currencys.Add(new Currency() { Final = row[2].ConvertTo<int>(), CurrencyAbbriviation = "EUR" });
                currencys.Add(new Currency() { Final = row[3].ConvertTo<int>(), CurrencyAbbriviation = "GBP" });
                currencys.Add(new Currency() { Final = row[4].ConvertTo<int>(), CurrencyAbbriviation = "RUB" });
                AppPrices appPrice = new AppPrices() { Id = (int)row[0], Prices = currencys, Discount = row[5].ConvertTo<int>(), SuccessTimestamp = row[6].ConvertTo<DateTime>() };
                appPrices.Add(appPrice);
            }
            return appPrices;
        }

        public LanguageModel SelectLanguageModel()
        {
            string query = "SELECT language.id, language.name, language.shortname FROM language ";
            var ds = DB.SelectDataSet(query);
            LanguageModel languageModel = new LanguageModel();
            languageModel.Create(ds);
            return languageModel;
        }

        public ChangesModel SelectChanges(int itemId = 0)
        {
            string query = BuildChangesQuery(itemId);
            var ds = DB.SelectDataSet(query);
            var changesModel = new ChangesModel();
            foreach (var row in ds)
            {
                int i = 0;
                var changeItem = new ChangeItem
                {
                    Id = row[i++].ConvertTo<int>(),
                    ItemId = row[i++].ConvertTo<int>(),
                    ChangeType = row[i++].ConvertTo<string>(),
                    Field = (row[i++].ConvertTo<string>()),
                    OldValue = row[i++].ConvertTo<string>(),
                    NewValue = row[i++].ConvertTo<string>(),
                    Timestamp = row[i].ConvertTo<DateTime>()
                };
                changeItem.TranslatedField = TranslateFieldName(changeItem.Field);
                changesModel.Changes.Add(changeItem);
            }
            return changesModel;
        }

        public LastUpdateTime SelectLastUpdate()
        {
            string query = BuildLastUpdateQuery();
            var ds = DB.SelectDataSet(query);
            var lastUpdateTime = new LastUpdateTime();
            lastUpdateTime.Id = 1;
            lastUpdateTime.Name = "Crossout Price Update";
            lastUpdateTime.Timestamp = ds.FirstOrDefault()[0].ConvertTo<DateTime>();
            return lastUpdateTime;
        }

        public List<BadgeExchangeDeal> SelectBadgeExchange(int language)
        {
            string query = BuildBadgeExchangeQuery();
            var ds = DB.SelectDataSet(query);
            var badgeExchangeDeals = new List<BadgeExchangeDeal>();
            var containedItems = new Dictionary<int, Item>();
            foreach(var row in ds)
            {
                var badgeExchangeDeal = new BadgeExchangeDeal();
                badgeExchangeDeal.Create(row);
                if (!containedItems.ContainsKey(badgeExchangeDeal.RewardItemId))
                {
                    var item = SelectItem(badgeExchangeDeal.RewardItemId, false, language).Item;
                    containedItems.Add(item.Id, item);
                    badgeExchangeDeal.RewardItem = item;
                }
                else
                {
                    badgeExchangeDeal.RewardItem = containedItems[badgeExchangeDeal.RewardItemId];
                }
                badgeExchangeDeals.Add(badgeExchangeDeal);
            }
            return badgeExchangeDeals;
        }

        public List<SnipeItem> SelectSnipeItems(int language)
        {
            string query = BuildSnipeQuery(language);
            var ds = DB.SelectDataSet(query);
            var snipeItems = new Dictionary<int, SnipeItem>();
            foreach (var row in ds)
            {
                var marketEntry = new MarketEntry();
                marketEntry.Create(row);
                if (!snipeItems.ContainsKey(marketEntry.ItemNumber))
                {
                    var me = marketEntry;
                    snipeItems.Add(me.ItemNumber, new SnipeItem { 
                        Id = me.ItemNumber,
                        Name = me.ItemName,
                        LocalizedName = me.ItemLocalizedName,
                        MarketEntries = new List<MarketEntry> { me }
                    });
                }
                else
                {
                    var me = snipeItems[marketEntry.ItemNumber].MarketEntries;
                    me.Add(marketEntry);
                }
            }
            foreach (var item in snipeItems)
            {
                item.Value.MarketEntries.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
                item.Value.CalculatePriceEdge();
            }
            return snipeItems.Values.ToList();
        }

        public List<ItemPoco> SelectSalvageItems(int language)
        {
            NPoco.Connection.Open();
            string query = "SELECT * FROM item LEFT JOIN itemlocalization ON itemlocalization.itemnumber = item.id LEFT JOIN rarity ON rarity.id = item.raritynumber WHERE item.removed = 0 AND item.meta = 0 AND itemlocalization.languagenumber = @0";
            var items = NPoco.Fetch<ItemPoco>(query, language);
            var ignoredCategories = new List<int> { 5, 7, 8, 9 };
            items = items.Where(x => !ignoredCategories.Contains(x.CategoryNumber)).ToList();
            NPoco.Connection.Close();
            return items;
        }

        public List<SalvageRewardPoco> SelectSalvageRewards(int language)
        {
            NPoco.Connection.Open();
            // [... , rarity.*, rarity.* ...] is mandatory
            var salvageRewards = NPoco.Fetch<SalvageRewardPoco>("SELECT salvagereward.*, item.*, itemlocalization.*, rarity.*, rarity.* FROM salvagereward LEFT JOIN rarity ON rarity.id = salvagereward.raritynumber LEFT JOIN item ON item.id = salvagereward.rewarditem LEFT JOIN itemlocalization ON itemlocalization.itemnumber = item.id WHERE itemlocalization.languagenumber = @0", language);
            NPoco.Connection.Close();
            return salvageRewards;
        }

        public List<FrontendLocalizationPoco> SelectFrontendLocalizations(int language, string category)
        {
            NPoco.Connection.Open();
            var locs = NPoco.Fetch<FrontendLocalizationPoco>("SELECT * FROM frontendlocalization LEFT JOIN language ON language.id = frontendlocalization.languagenumber WHERE frontendlocalization.languagenumber = @0 AND (frontendlocalization.category = @1 OR frontendlocalization.category = @2)", language, "shared", category);
            NPoco.Connection.Close();
            return locs;
        }

        public List<string> SelectMatchTypes()
        {
            NPoco.Connection.Open();
            var types = NPoco.Fetch<string>("SELECT DISTINCT match_type FROM crossout.cod_match_records");
            NPoco.Connection.Close();
            return types;
        }

        public List<MapPoco> SelectMaps()
        {
            NPoco.Connection.Open();
            var maps = NPoco.Fetch<MapPoco>();
            NPoco.Connection.Close();
            return maps;
        }

        public MapPoco SelectMap(string name)
        {
            NPoco.Connection.Open();
            var map = NPoco.SingleById<MapPoco>(name);
            NPoco.Connection.Close();
            return map;
        }

        public List<MatchHistoryEntryPoco> SelectMatchHistoryEntries(DateTime matchStartFrom, DateTime matchStartTo, string[] types, string[] maps, int powerScore)
        {
            var typesString = types.Length > 0 ? "AND match_type IN (@2)" : "";
            var mapsString = maps.Length > 0 ? "AND cod_match_records.map_name IN (@3)" : "";
            var powerScoreString = powerScore > 0 ? "AND min_power_score < @4 AND max_power_score > @4" : "";
            NPoco.Connection.Open();
            var matchRecords = NPoco.Fetch<MatchHistoryEntryPoco>($"SELECT * FROM cod_match_records LEFT JOIN cod_maps ON cod_match_records.map_name = cod_maps.map_name WHERE match_start >= @0 AND match_start <= @1 {typesString} {mapsString} {powerScoreString} ORDER BY match_id DESC LIMIT 5000", matchStartFrom, matchStartTo, types, maps, powerScore);
            NPoco.Connection.Close();
            return matchRecords;
        }

        public MatchPoco SelectMatchRecord(long id)
        {
            NPoco.Connection.Open();
            var matchRecord = NPoco.SingleById<MatchPoco>(id);
            NPoco.Connection.Close();
            return matchRecord;
        }

        public List<RoundPoco> SelectRoundRecords(long matchId)
        {
            NPoco.Connection.Open();
            var roundRecords = NPoco.Fetch<RoundPoco>("WHERE match_id = @0", matchId);
            NPoco.Connection.Close();
            return roundRecords;
        }


        public List<RoundDamage> SelectRoundDamage(long matchId, int lang)
        {
            NPoco.Connection.Open();
            var roundDamages = NPoco.Fetch<RoundDamage>("SELECT * FROM crossout.cod_player_round_damage_records LEFT JOIN item ON cod_player_round_damage_records.weapon = item.externalKey LEFT JOIN itemlocalization ON item.id = itemlocalization.itemnumber WHERE match_id = @0 AND (itemlocalization.languagenumber = @1 OR cod_player_round_damage_records.weapon = 'Ramming');", matchId, lang);
            NPoco.Connection.Close();
            return roundDamages;
        }

        public List<MatchMedal> SelectMatchMedal(long matchId)
        {
            NPoco.Connection.Open();
            var matchMedals = NPoco.Fetch<MatchMedal>("SELECT * FROM crossout.cod_player_match_medals WHERE match_id = @0", matchId);
            NPoco.Connection.Close();
            return matchMedals;
        }

        public List<PlayerRoundPoco> SelectPlayerRoundRecords(long matchId)
        {
            NPoco.Connection.Open();
            var playerRoundRecords = NPoco.Fetch<PlayerRoundPoco>("WHERE match_id = @0", matchId);
            NPoco.Connection.Close();
            return playerRoundRecords;
        }

        public bool ValidUID(int uid)
        {
            bool valid_uid = false;
            NPoco.Connection.Open();

            if (NPoco.Fetch<PlayerRoundPoco>("WHERE uid = @0", uid).Any())
                valid_uid = true;

            NPoco.Connection.Close();
            return valid_uid;
        }

        public List<string> SelectNicknames(int uid)
        {
            NPoco.Connection.Open();
            List<string> player_nickname = NPoco.Fetch<PlayerRoundPoco>("WHERE uid = @0", uid).Select(x => x.nickname).ToList() ?? new List<string> { "Invalid UID" };
            NPoco.Connection.Close();
            return player_nickname;
        }

        public int SelectRecordedCount(int uid)
        {
            NPoco.Connection.Open();
            int player_nickname = NPoco.ExecuteScalar<int>("SELECT count(distinct record.match_id) FROM crossout.cod_match_records record INNER JOIN crossout.cod_player_round_records player ON record.match_id = player.match_id WHERE player.uid = @0 AND match_type <> 'Custom Game'", uid);
            NPoco.Connection.Close();
            return player_nickname;
        }

        public int SelectUploadedCount(int uid)
        {
            NPoco.Connection.Open();
            int player_nickname = NPoco.ExecuteScalar<int>("SELECT count(*) FROM crossout.cod_upload_records WHERE uid = @0", uid);
            NPoco.Connection.Close();
            return player_nickname;
        }

        public int SelectPvpGameCount(int uid)
        {
            NPoco.Connection.Open();
            int player_nickname = NPoco.ExecuteScalar<int>("SELECT count(distinct record.match_id) FROM crossout.cod_match_records record INNER JOIN crossout.cod_player_round_records player ON record.match_id = player.match_id WHERE player.uid = @0 AND record.match_classification = 1", uid);
            NPoco.Connection.Close();
            return player_nickname;
        }

        public int SelectWinCount(int uid)
        {
            NPoco.Connection.Open();
            int player_nickname = NPoco.ExecuteScalar<int>("SELECT count(distinct record.match_id) FROM crossout.cod_match_records record INNER JOIN crossout.cod_player_round_records player ON record.match_id = player.match_id WHERE player.uid = @0 AND match_type <> 'Custom Game' and record.winning_team = player.team", uid);
            NPoco.Connection.Close();
            return player_nickname;
        }

        public int SelectSecondsPlayed(int uid)
        {
            NPoco.Connection.Open();
            int seconds_played = NPoco.ExecuteScalar<int>("SELECT SUM(TO_SECONDS(round.round_end) - TO_SECONDS(round.round_start)) FROM crossout.cod_round_records round INNER JOIN crossout.cod_player_round_records player ON round.match_id = player.match_id WHERE player.uid = @0", uid);
            NPoco.Connection.Close();
            return seconds_played;
        }

        public int SelectPVPKillAssists(int uid)
        {
            NPoco.Connection.Open();
            int kill_and_assists = NPoco.ExecuteScalar<int>("SELECT sum(player.kills + player.assists) FROM crossout.cod_match_records record INNER JOIN crossout.cod_player_round_records player ON record.match_id = player.match_id WHERE player.uid = @0 AND record.match_classification = 1", uid);
            NPoco.Connection.Close();
            return kill_and_assists;
        }

        public int SelectMVPCount(int uid)
        {
            NPoco.Connection.Open();
            int mvp_count = NPoco.ExecuteScalar<int>("SELECT sum(amount) FROM crossout.cod_match_records record INNER JOIN crossout.cod_player_match_medals medals ON record.match_id = medals.match_id WHERE medals.uid = @0 AND record.match_classification = 1 AND medals.medal = 'PvpMvpWin'", uid);
            NPoco.Connection.Close();
            return mvp_count;
        }

        public OverviewCharts SelectOverviewBreakdowns(int uid)
        {
            OverviewCharts overview_dropdowns = new OverviewCharts { };

            NPoco.Connection.Open();
            overview_dropdowns.gamemode_preference = PopulateGameModeDrillDown(uid);
            overview_dropdowns.weapon_preference = PopulatePartDrillDown(uid, "Weapons", "ALL");
            overview_dropdowns.movement_preference = PopulatePartDrillDown(uid, "Movement", "ALL");
            NPoco.Connection.Close();

            return overview_dropdowns;
        }

        public List<DrillDown> PopulatePartDrillDown(int uid, string category, string match_type)
        {
            List<DrillDown> drill_down_return = new List<DrillDown> { };

            if (match_type == "ALL")
            {
                drill_down_return = NPoco.Fetch<DrillDown>(@"SELECT ocr.type, ocr.name, count(distinct record.match_id) as count
                                                                     FROM crossout.cod_match_records record
                                                               INNER JOIN crossout.cod_player_round_records player ON record.match_id = player.match_id
                                                               INNER JOIN crossout.cod_builds build ON player.build_hash = build.build_hash and player.power_score = build.power_score
                                                               INNER JOIN crossout.cod_build_parts part ON build.build_id = part.build_id
                                                               INNER JOIN crossout.item item ON part.part_name = item.externalKey
                                                               INNER JOIN crossout.ocrstats ocr ON item.id = ocr.itemnumber
                                                                    WHERE player.uid = @0
                                                                      AND ocr.category = @1
                                                                      AND record.match_type <> 'Custom Game'
                                                                    GROUP BY ocr.type, ocr.name
                                                                    ORDER BY count DESC", uid, category);
            }
            else
            {
                drill_down_return = NPoco.Fetch<DrillDown>(@"SELECT ocr.type, ocr.name, COUNT(distinct record.match_id) as count
                                                                     FROM crossout.cod_match_records record
                                                               INNER JOIN crossout.cod_player_round_records player ON record.match_id = player.match_id
                                                               INNER JOIN crossout.cod_builds build ON player.build_hash = build.build_hash and player.power_score = build.power_score
                                                               INNER JOIN crossout.cod_build_parts part ON build.build_id = part.build_id
                                                               INNER JOIN crossout.item item ON part.part_name = item.externalKey
                                                               INNER JOIN crossout.ocrstats ocr ON item.id = ocr.itemnumber
                                                                    WHERE player.uid = @0
                                                                      AND ocr.category = @1
                                                                      AND record.match_type = @2
                                                                    GROUP BY ocr.type, ocr.name
                                                                    ORDER BY count DESC", uid, category, match_type);
            }
            return drill_down_return;
        }

        public List<DrillDown> PopulateGameModeDrillDown(int uid)
        {
            List<DrillDown> drill_down_return = new List<DrillDown> { };

            drill_down_return = NPoco.Fetch<DrillDown>(@"SELECT CASE record.match_classification 
			                                                          WHEN 1 THEN 'PvP'
			                                                          WHEN 2 THEN 'PvE'
			                                                          WHEN 3 THEN 'Brawl'
			                                                          WHEN 4 THEN 'Bedlam'
			                                                          WHEN 5 THEN 'Custom'
			                                                                 ELSE 'Undefined' END as type, 
                                                                      record.match_type as name, COUNT(distinct record.match_id) as count
		                                                         FROM crossout.cod_match_records record
                                                           INNER JOIN crossout.cod_player_round_records player ON record.match_id = player.match_id
		                                                        WHERE player.uid = @0
		                                                        GROUP BY record.match_classification, record.match_type
                                                                ORDER BY COUNT(DISTINCT record.match_id) DESC", uid);

            return drill_down_return;
        }

        public GameModeDetail PopulateGameModeDetail(int uid)
        {
            GameModeDetail game_mode_detail = new GameModeDetail { game_modes = new List<GameMode> { } };

            NPoco.Connection.Open();
            game_mode_detail.game_modes = PopulateGameModeDetailList(uid);
            NPoco.Connection.Close();

            return game_mode_detail;
        }

        public List<GameMode> PopulateGameModeDetailList(int uid)
        {
            List<GameMode> game_modes = new List<GameMode> { };

            game_modes = NPoco.Fetch<GameMode>(@"SELECT CASE record.match_classification 
						                                WHEN 1 THEN 'PvP'
						                                WHEN 2 THEN 'PvE'
						                                WHEN 3 THEN 'Brawl'
						                                WHEN 4 THEN 'Bedlam'
						                                WHEN 5 THEN 'Custom'
								                                ELSE 'Undefined' END as match_classification, 
		                                record.match_type, COUNT(DISTINCT record.match_id) as games, 
		                                COUNT(*) as rounds, count(distinct wins.match_id) as wins,  SUM(TO_SECONDS(record.match_end) - TO_SECONDS(record.match_start)) as time_spent,
		                                0 as medals, IFNULL(SUM(mvp.amount),0) as mvp, SUM(player.kills) as kills, SUM(player.assists) as assists, SUM(player.drone_kills) as drone_kills, SUM(player.deaths) as deaths, 
		                                SUM(player.damage) as damage, SUM(player.damage_taken) as damage_rec, SUM(player.score) as score
		                                FROM crossout.cod_match_records record
                                    INNER JOIN crossout.cod_player_round_records player on record.match_id = player.match_id
                                    inner join crossout.cod_round_records round on record.match_id = round.match_id
                                    left join crossout.cod_player_match_medals mvp on record.match_id = mvp.match_id and round.round_id = mvp.round_id and player.uid = mvp.uid and mvp.medal = 'PvpMvpWin'
                                    LEFT JOIN crossout.cod_match_records wins on record.match_id = wins.match_id and player.team = wins.winning_team
                                    WHERE player.uid = @0
                                    GROUP BY record.match_type
                                    ORDER BY record.match_classification", uid);

            return game_modes;
        }

        public MatchHistoryDetail PopulateHistoryDetail(int uid)
        {
            MatchHistoryDetail history_detail = new MatchHistoryDetail { match_history = new List<UserMatchHistory> { } };

            NPoco.Connection.Open();
            history_detail.match_history = PopulateHistoryDetailList(uid);
            NPoco.Connection.Close();

            return history_detail;
        }

        public List<UserMatchHistory> PopulateHistoryDetailList(int uid)
        {
            List<UserMatchHistory> game_modes = new List<UserMatchHistory> { };

            game_modes = NPoco.Fetch<UserMatchHistory>(@"SELECT record.match_id, 
		                                                    CASE record.match_classification WHEN 1 THEN 'PvP' WHEN 2 THEN 'PvE' WHEN 3 THEN 'Brawl' WHEN 4 THEN 'Bedlam' WHEN 5 THEN 'Custom' ELSE 'Undefined' END as match_classification, 
                                                            record.match_type, record.match_start, record.match_end, record.map_name as map, 
                                                            player.power_score, player.kills, player.assists, player.drone_kills, player.damage, player.damage_taken as damage_rec, 
	                                                        CASE record.winning_team WHEN player.team THEN 'Win' WHEN 0 THEN 'Draw' ELSE 'Loss' END as result,
                                                            GROUP_CONCAT(DISTINCT CONCAT(resource.resource,':',resource.amount) SEPARATOR ',') AS resources 
	                                                   FROM crossout.cod_match_records record
                                                 INNER JOIN crossout.cod_player_round_records player ON record.match_id = player.match_id
                                                  LEFT JOIN crossout.cod_player_match_resources resource ON record.match_id = resource.match_id AND player.uid = resource.uid
	                                                  WHERE player.uid = @0
                                                      GROUP BY record.match_id
                                                      ORDER BY match_id DESC
                                                      LIMIT 1000;", uid);

            return game_modes;
        }

        public string TranslateFieldName(string toTranslate)
        {
            switch (toTranslate)
            {
                case "name":
                    return "Name";
                case "rarity":
                    return "Rarity";
                case "category":
                    return "Category";
                case "type":
                    return "Type";
                case "removed":
                    return "Removed Flag";
                case "recipe":
                    return "Recipe";
                case "ingredient":
                    return "Ingredient";
                case "item":
                    return "Item";
                default:
                    return toTranslate;
            }
        }

        public static List<FactionModel> CreateAllFactionsForEdit(List<object[]> data)
        {
            List<FactionModel> items = new List<FactionModel>();
            foreach (var row in data)
            {
                FactionModel item = new FactionModel
                {
                    Id = row[0].ConvertTo<int>(),
                    Name = row[1].ConvertTo<string>()
                };
                items.Add(item);
            }
            return items;
        }

        public void SaveRecipe(EditModelSave editModelSave, List<EditItem> items)
        {
            if (editModelSave.RecipeNumber == 0 && items.Any(x => x.Id > 0))
            {
                var result = DB.Insert("recipe", new string[] { "itemnumber", "factionnumber" }, new object[] { editModelSave.ItemNumber, editModelSave.FactionNumber });
                editModelSave.RecipeNumber = (int)result.LastInsertedId;
                RecordChange(editModelSave.ItemNumber, "ADD", "recipe");
            }

            foreach (var item in items)
            {
                if (item.OldId > 0)
                {
                    if (item.Id > 0)
                    {
                        // New Item Id is above 0 so we update this item
                        List<Parameter> parameters = new List<Parameter>();
                        parameters.Add(new Parameter
                        {
                            Identifier = "@recipenumberWhere",
                            Value = editModelSave.RecipeNumber
                        });
                        parameters.Add(new Parameter { Identifier = "@recipeitemnumber", Value = item.RecipeItemNumber });

                        var rs = DB.Update("recipeitem",
                            new string[] { "itemnumber", "number" },
                            new object[] { item.Id, item.Number },
                            "recipenumber = @recipenumberWhere AND recipeitem.id = @recipeitemnumber",
                            parameters);

                        if (item.OldId != item.Id)
                        {
                            RecordChange(editModelSave.ItemNumber, "UPDATE", "ingredient", item.OldId.ToString(), item.Id.ToString());
                        }

                    }
                    else
                    {
                        // New Item Id is 0 (or below) we delete this item
                        List<Parameter> parameters = new List<Parameter>();
                        parameters.Add(new Parameter
                        {
                            Identifier = "@recipenumber",
                            Value = editModelSave.RecipeNumber
                        });
                        parameters.Add(new Parameter { Identifier = "@recipeitemnumber", Value = item.RecipeItemNumber });
                        var result =
                            DB.ExecuteSQL(
                                "DELETE FROM recipeitem WHERE recipeitem.recipenumber = @recipenumber AND recipeitem.id = @recipeitemnumber;",
                                parameters);
                        RecordChange(editModelSave.ItemNumber, "DELETE", "ingredient", item.OldId.ToString());

                        if (!items.Any(x => x.Id != 0))
                        {
                            List<Parameter> parameters2 = new List<Parameter>();
                            parameters2.Add(new Parameter
                            {
                                Identifier = "@recipenumber",
                                Value = editModelSave.RecipeNumber
                            });
                            var result2 =
                                DB.ExecuteSQL(
                                    "DELETE FROM recipe WHERE recipe.id = @recipenumber;",
                                    parameters);
                            RecordChange(editModelSave.ItemNumber, "DELETE", "recipe");
                        }
                    }
                }
                if (item.OldId == 0)
                {
                    if (item.Id > 0 && item.Number > 0)
                    {
                        DB.Insert("recipeitem", new string[] { "recipenumber", "itemnumber", "number" },
                            new object[] { editModelSave.RecipeNumber, item.Id, item.Number });
                        RecordChange(editModelSave.ItemNumber, "ADD", "ingredient", "", item.Id.ToString());
                    }
                }
            }

            if (editModelSave.OldFactionNumber > 0 && editModelSave.FactionNumber > 0 && editModelSave.FactionNumber != editModelSave.OldFactionNumber)
            {
                List<Parameter> parameters = new List<Parameter>();
                parameters.Add(new Parameter { Identifier = "@factionnumber", Value = editModelSave.FactionNumber });
                parameters.Add(new Parameter { Identifier = "@recipenumber", Value = editModelSave.RecipeNumber });
                var result = DB.ExecuteSQL("UPDATE recipe SET recipe.factionnumber = @factionnumber WHERE recipe.id = @recipenumber", parameters);
            }
        }

        public void SaveGeneralItemInfo(EditGeneralInfo info, EditModelSave editModelSave)
        {
            var item = SelectItem(editModelSave.ItemNumber, false, 1);

            if (item.Item.Name != info.NewItemName)
            {
                RecordChange(item.Item.Id, "UPDATE", "name", item.Item.Name, info.NewItemName);
            }

            if (item.Item.RarityId != info.NewRarity)
            {
                RecordChange(item.Item.Id, "UPDATE", "rarity", item.Item.RarityId.ToString(), info.NewRarity.ToString());
            }

            if (item.Item.CategoryId != info.NewCategory)
            {
                RecordChange(item.Item.Id, "UPDATE", "category", item.Item.CategoryId.ToString(), info.NewCategory.ToString());
            }

            if (item.Item.TypeId != info.NewType)
            {
                RecordChange(item.Item.Id, "UPDATE", "type", item.Item.TypeId.ToString(), info.NewType.ToString());
            }

            if (item.Item.Removed != Convert.ToInt32(info.NewRemovedStatus))
            {
                RecordChange(item.Item.Id, "UPDATE", "removed", Convert.ToString(item.Item.Removed), Convert.ToString(Convert.ToInt32(info.NewRemovedStatus)));
            }

            int removed = 0;
            if (info.NewRemovedStatus)
                removed = 1;
            List<Parameter> parameters = new List<Parameter>();
            parameters.Add(new Parameter { Identifier = "@id", Value = editModelSave.ItemNumber });
            parameters.Add(new Parameter { Identifier = "@locname", Value = info.NewItemName });
            parameters.Add(new Parameter { Identifier = "@rarity", Value = info.NewRarity });
            parameters.Add(new Parameter { Identifier = "@category", Value = info.NewCategory });
            parameters.Add(new Parameter { Identifier = "@type", Value = info.NewType });
            parameters.Add(new Parameter { Identifier = "@removed", Value = removed });
            var result = DB.ExecuteSQL("UPDATE item SET item.name = @locname, item.raritynumber = @rarity, item.categorynumber = @category, item.typenumber = @type, item.removed = @removed WHERE item.id = @id", parameters);
        }

        public void RecordChange(int itemId, string type, string field, string oldValue = "", string newValue = "")
        {
            List<Parameter> parameters = new List<Parameter>();
            parameters.Add(new Parameter { Identifier = "@itemid", Value = itemId });
            parameters.Add(new Parameter { Identifier = "@type", Value = type });
            parameters.Add(new Parameter { Identifier = "@field", Value = field });
            parameters.Add(new Parameter { Identifier = "@oldValue", Value = oldValue });
            parameters.Add(new Parameter { Identifier = "@newValue", Value = newValue });
            parameters.Add(new Parameter { Identifier = "@datetime", Value = DateTime.UtcNow });
            var result = DB.ExecuteSQL("INSERT INTO changes SET changes.itemid = @itemid, changes.changetype = @type, changes.field = @field,  changes.oldValue = @oldValue,  changes.newValue = @newValue, changes.datetime = @datetime", parameters);
        }

        public static string BuildStatusQuery()
        {
            string query = "SELECT item.id,item.datetime as datetime FROM item ORDER BY item.datetime DESC LIMIT 1;";
            return query;
        }

        public static string BuildRecipeQuery()
        {
            string selectColumns = "item.id,item.name,item.sellprice,item.buyprice,item.selloffers,item.buyorders,item.datetime,rarity.id,rarity.name,category.id,category.name,type.id,type.name,recipe2.id,recipeitem.number,recipeitem.id,recipe.factionnumber,faction.name,recipe2.amount,item.amount,itemlocalization.localizedname";
            string query =
                $"SELECT {selectColumns} " +
                "FROM recipe " +
                "LEFT JOIN recipeitem ON recipeitem.recipenumber = recipe.id " +
                "LEFT JOIN item ON item.id = recipeitem.itemnumber " +
                "LEFT JOIN rarity ON rarity.id = item.raritynumber " +
                "LEFT JOIN category ON category.id = item.categorynumber " +
                "LEFT JOIN type ON type.id = item.typenumber " +
                "LEFT JOIN recipe recipe2 ON recipe2.itemnumber = recipeitem.itemnumber " +
                "LEFT JOIN faction faction ON faction.id = recipe.factionnumber " +
                "LEFT JOIN itemlocalization ON itemlocalization.itemnumber = item.id AND itemlocalization.languagenumber = @language " +
                "WHERE recipe.id = @id";

            return query;
        }

        public static string BuildIngredientUsageQuery()
        {
            string collumns = "recipe.id, recipe.itemnumber, recipeitem.number";
            string tables = "crossout.recipeitem LEFT JOIN recipe ON recipeitem.recipenumber = recipe.id";
            string query = $"SELECT {collumns} FROM {tables} WHERE recipeitem.itemnumber = @itemnumber";
            return query;
        }

        public static string BuildSearchQuery(bool hasFilter, bool limit, bool count, bool hasId, bool hasRarity, bool hasCategory, bool hasFaction, bool showRemovedItems, bool showMetaItems, bool rmdItemsOnly)
        {
            string selectColumns = "item.id,item.name,item.sellprice,item.buyprice,item.selloffers,item.buyorders,item.datetime,rarity.id,rarity.name,category.id,category.name,type.id,type.name,recipe.id,item.removed,item.meta,faction.id,faction.name,item.popularity,item.workbenchrarity,item.craftingsellsum,item.craftingbuysum,item.amount,recipe.amount,itemlocalization.localizedname";
            if (count)
            {
                selectColumns = "count(*)";
            }
            string query = $"SELECT {selectColumns} FROM item LEFT JOIN rarity on rarity.id = item.raritynumber LEFT JOIN category on category.id = item.categorynumber LEFT JOIN type on type.id = item.typenumber LEFT JOIN recipe ON recipe.itemnumber = item.id LEFT JOIN faction ON faction.id = recipe.factionnumber LEFT JOIN itemlocalization ON itemlocalization.itemnumber = item.id AND itemlocalization.languagenumber = @language ";

            if (!hasId)
            {
                if (hasFilter)
                {
                    query += "WHERE (itemlocalization.localizedname LIKE @filter OR item.name LIKE @filter) ";
                }
                else
                {
                    query += "WHERE 1=1 ";
                }
            }
            else
            {
                query += "WHERE item.id = @id ";
            }

            //query += " AND itemlocalization.languagenumber = @language ";

            if (hasRarity)
            {
                query += " AND rarity.id = @rarity ";
            }

            if (hasCategory)
            {
                query += " AND category.id = @category ";
            }

            if (hasFaction)
            {
                query += " AND faction.id = @faction ";
            }

            if (!showRemovedItems)
            {
                if (rmdItemsOnly)
                {
                    query += " AND item.removed = 1 ";
                }
                else
                {
                    query += " AND item.removed = 0 ";
                }
            }

            if (!showMetaItems)
            {
                query += " AND item.meta = 0 ";
            }

            if (!count)
            {
                query += "ORDER BY item.id asc, item.name asc ";
            }

            //if (limit)
            //{
            //    query += "LIMIT @from,@to";
            //}

            return query;
        }

        public static string BuildAllActiveItemsQuery(bool excludeRemovedItems = true)
        {
            string conditions = "";
            if (excludeRemovedItems)
            {
                conditions = "WHERE removed = 0";
            }
            string query = $"SELECT item.id,item.name,itemlocalization.localizedname FROM item LEFT JOIN itemlocalization ON itemlocalization.itemnumber = item.id AND itemlocalization.languagenumber = @language {conditions} ORDER BY item.name ASC,item.id ASC;";
            return query;
        }

        public static string BuildFactionsQuery()
        {
            string query = "SELECT faction.id, faction.name FROM faction ORDER BY id ASC;";
            return query;
        }

        public static string BuildRarityQuery()
        {
            string query = "SELECT rarity.id, rarity.name FROM rarity ORDER BY rarity.order ASC;";
            return query;
        }

        public static string BuildCategoryQuery()
        {
            string query = "SELECT category.id, category.name FROM category ORDER BY id ASC;";
            return query;
        }

        public static string BuildTypeQuery()
        {
            string query = "SELECT type.id, type.name FROM type ORDER BY id ASC;";
            return query;
        }

        public static string BuildItemsQueryFromIDList(List<int> ids)
        {
            StringBuilder sb = new StringBuilder();
            string query = "SELECT item.id, item.name, item.sellprice, item.buyprice, item.amount, itemlocalization.localizedname FROM item LEFT JOIN itemlocalization ON itemlocalization.itemnumber = item.id AND itemlocalization.languagenumber = @language WHERE ";
            sb.Append(query);
            int i = 0;
            foreach (var id in ids)
            {
                if (i == 0)
                {
                    sb.Append("item.id=");
                    sb.Append(id);
                }
                else
                {
                    sb.Append(" OR item.id=");
                    sb.Append(id);
                }
                i++;
            }
            query = sb.ToString();
            return query;
        }

        public static string BuildSteamPricesQuery()
        {
            string collumns = "steamprices.appid,steamprices.priceusd,steamprices.priceeur,steamprices.pricegbp,steamprices.pricerub,steamprices.discount,steamprices.successtimestamp";
            string query = $"SELECT {collumns} FROM steamprices";
            return query;
        }

        public static string BuildCraftingOverviewQuery()
        {
            string collumns = "item.id,item.name,item.sellprice,item.buyprice,item.selloffers,item.buyorders,item.datetime,rarity.id,rarity.name,category.id,category.name,type.id,type.name,recipe.id,item.removed,faction.id,faction.name,item.popularity,item.workbenchrarity,item.craftingsellsum,item.craftingbuysum,item.amount";
            string tables = "item LEFT JOIN rarity on rarity.id = item.raritynumber LEFT JOIN category on category.id = item.categorynumber LEFT JOIN type on type.id = item.typenumber LEFT JOIN recipe ON recipe.itemnumber = item.id LEFT JOIN faction ON faction.id = recipe.factionnumber";
            string query = $"SELECT {collumns} FROM {tables} WHERE removed=0 AND meta=0 AND craftingsellsum!=0 AND craftingbuysum!=0 ORDER BY item.id";
            return query;
        }

        public static string BuildHtmlExport(bool includeRemovedItems)
        {
            string collumns = "item.id,item.name,item.sellprice,item.buyprice,item.selloffers,item.buyorders,item.datetime,rarity.id,rarity.name,category.id,category.name,type.id,type.name,recipe.id,item.removed,item.meta,faction.id,faction.name,item.popularity,item.workbenchrarity,item.craftingsellsum,item.craftingbuysum,item.amount,recipe.amount,itemlocalization.localizedname";
            string tables = "item LEFT JOIN rarity on rarity.id = item.raritynumber LEFT JOIN category on category.id = item.categorynumber LEFT JOIN type on type.id = item.typenumber LEFT JOIN recipe ON recipe.itemnumber = item.id LEFT JOIN faction ON faction.id = recipe.factionnumber LEFT JOIN itemlocalization ON itemlocalization.itemnumber = item.id AND itemlocalization.languagenumber = 1";
            string whereClauseRemovedItems = includeRemovedItems ? "" : "removed=0 AND ";
            string query = $"SELECT {collumns} FROM {tables} WHERE {whereClauseRemovedItems}meta=0 ORDER BY item.id";
            return query;
        }

        public static string BuildChangesQuery(int itemId = 0)
        {
            string collumns = "changes.id,changes.itemid,changes.changetype,changes.field,changes.oldvalue,changes.newvalue,changes.datetime";
            string tables = "changes";
            string query;
            if (itemId != 0)
            {
                query = $"SELECT {collumns} FROM {tables} WHERE changes.itemid = {itemId} ORDER BY changes.id DESC LIMIT 100";
            }
            else
            {
                query = $"SELECT {collumns} FROM {tables} ORDER BY changes.id DESC LIMIT 500";
            }

            return query;
        }

        public static string BuildTrendsQuery(DateTime time)
        {
            string collumns = "market.itemnumber, market.sellprice, market.buyprice, market.selloffers, market.buyorders, market.datetime";
            string tables = "market";
            string query = $"SELECT {collumns} FROM {tables} WHERE market.datetime = '{time.ToString("yyyy-MM-dd HH:mmm:ss")}'";
            return query;
        }

        public static string BuildLastUpdateQuery()
        {
            string collumns = "item.datetime";
            string tables = "item";
            string query = $"SELECT {collumns} FROM {tables} ORDER BY datetime DESC LIMIT 1";
            return query;
        }

        public static string BuildBadgeExchangeQuery()
        {
            string collumns = "badgeexchange.id, badgeexchange.rewarditem, badgeexchange.rewardamount, badgeexchange.badgecost, badgeexchange.active, badgeexchange.lastbeginactive";
            string tables = "badgeexchange";
            string query = $"SELECT {collumns} FROM {tables}";
            return query;
        }

        public static string BuildSnipeQuery(int lang)
        {
            string collumns = "marketrecent.id, marketrecent.itemnumber, marketrecent.sellprice, marketrecent.buyprice, marketrecent.selloffers, marketrecent.buyorders, marketrecent.datetime, item.name, itemlocalization.localizedname";
            string tables = "marketrecent";
            string whereClause = $"marketrecent.datetime >= (SELECT datetime FROM (SELECT DISTINCT datetime FROM marketrecent ORDER BY datetime DESC LIMIT 5) as marketrecenttop2 ORDER BY datetime ASC LIMIT 1) AND itemlocalization.languagenumber={lang}";
            string join = "LEFT JOIN itemlocalization ON itemlocalization.itemnumber = marketrecent.itemnumber LEFT JOIN item ON item.id = marketrecent.itemnumber";
            string query = $"SELECT {collumns} FROM {tables} {join} WHERE {whereClause} ORDER BY id DESC;";
            return query;
        }
    }
}
