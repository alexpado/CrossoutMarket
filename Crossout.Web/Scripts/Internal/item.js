﻿

$('#reset').click(function (e) {
    $("#query").val('');
    $("#form").submit();
});

var recipeData = {
    loaded: false,
    data: {}
};


function updateTree(classname, recipe, uniqueid, show) {
    if (classname !== 'recipe-0') {
        $('.' + classname).each(function (i, obj) {
            var currentUniqueid = $(this).data('uniqueid');
            var currentParentUniqueid = $(this).data('parentuniqueid');
            var currentRecipe = $(this).data('recipe');
            var classname2 = 'recipe-' + $(this).data('recipe');
            if (currentParentUniqueid === uniqueid) {
                if (show) {
                    $(this).show();
                    //$('#shopping-list-wrapper').show();
                } else {
                    $(this).hide();
                    //$('#shopping-list-wrapper').hide();
                    $(this).find('button').removeClass('glyphicon-minus').addClass('glyphicon-plus');
                    window.updateTree(classname2, currentRecipe, currentUniqueid, show);
                }
            }
        });
    }
}

function toPrice(number) {
    return (number / 100.0).toFixed(2);
}

function toFixed(number) {
    return number.toFixed(2);
}

function updateSums(recipe, uniqueid) {
    if (recipeData.loaded) {

        $('#shopping-list > tbody').remove();

        $('.sum-row:visible').each(function (j, obj) {
            var sumuniqueid = $(this).data('uniqueid');

            var root = recipeData.data.Recipe.Recipe;
            var mainItem = findSumItem(root, sumuniqueid);
            var sumItem = mainItem.IngredientSum;
            var result = { items: new Array(), map: {}, shoppinglist: {} }

            if (sumItem !== null) {
                updateSum(root, mainItem, result, recipe);

                var sumBuy = 0;
                var sumSell = 0;
                var list = result.shoppinglist;
                var number;
                var item;
                var rec;
                for (var k in list) {
                    if (list.hasOwnProperty(k)) {
                        rec = list[k].item;
                        item = rec.Item;
                        number = list[k].number;
                        var sell = item.SellPrice;
                        var buy = item.BuyPrice;

                        sumSell += filterResourcePrice(item.Id, sell) * number;
                        sumBuy += filterResourcePrice(item.Id, buy) * number;
                    }
                }

                var sellPrice = mainItem.Item.SellPrice * Math.max(mainItem.RootNumber, 1);
                var buyPrice = mainItem.Item.BuyPrice * Math.max(mainItem.RootNumber, 1);

                var sellFeePrice = sellPrice * 0.9;
                var buyFeePrice = buyPrice * 0.9;

                var sellProfit = sellFeePrice - sumSell;
                var buyProfit = buyFeePrice - sumBuy;
                var sellBuyProfit = sellFeePrice - sumBuy;

                var sellClass = sellProfit > 0 ? 'sum-pos' : 'sum-neg';
                var buyClass = buyProfit > 0 ? 'sum-pos' : 'sum-neg';
                var sellBuyClass = sellBuyProfit > 0 ? 'sum-pos' : 'sum-neg';

                // Please if someone has a way to avoid this mess without huge frameworks like angular or react message me :)

                $('#uniqueid-' + sumItem.UniqueId).find('.sum-sell-fee').text(toPrice(sellFeePrice));
                $('#uniqueid-' + sumItem.UniqueId).find('.sum-buy-fee').text(toPrice(buyFeePrice));

                $('#uniqueid-' + sumItem.UniqueId).find('.sum-sell').text(toPrice(-sumSell));
                $('#uniqueid-' + sumItem.UniqueId).find('.sum-buy').text(toPrice(-sumBuy));

                $('#uniqueid-' + sumItem.UniqueId).find('.sum-sell-diff').removeClass('sum-neg').
                    removeClass('sum-pos').addClass(sellClass).text(toPrice(sellProfit));
                $('#uniqueid-' + sumItem.UniqueId).find('.sum-buy-diff').removeClass('sum-neg').
                    removeClass('sum-pos').addClass(buyClass).text(toPrice(buyProfit));
                $('#uniqueid-' + sumItem.UniqueId).find('.sum-sell-buy-diff').removeClass('sum-neg').
                    removeClass('sum-pos').addClass(sellBuyClass).text(toPrice(sellBuyProfit));

                if (mainItem.UniqueId === root.UniqueId) {
                    for (var key in result.shoppinglist) {
                        if (result.shoppinglist.hasOwnProperty(key)) {
                            rec = result.shoppinglist[key].item;
                            item = rec.Item;
                            number = result.shoppinglist[key].number;
                            $('#shopping-list').append(
                                '<tr data-item-id="' +
                                item.Id +
                                '"><td>' +
                                htmlName(item) +
                                '</td><td>' +
                                htmlRarity(item) +
                                '</td><td>' +
                                htmlNumberInput(number, 'input-number-' + item.Id) +
                                '</td><td>' +
                                htmlPriceInput(toPrice(item.SellPrice), 'input-sell-' + item.Id) +
                                '</td><td>' +
                                '' +
                                '</td><td>' +
                                htmlPriceInput(toPrice(item.BuyPrice), 'input-buy-' + item.Id) +
                                '</td></tr>');
                            $('#input-sell-' + item.Id).on('input',
                                function (e) {
                                    calculateShoppingList(root, result.shoppinglist);
                                });
                            $('#input-buy-' + item.Id).on('input',
                                function (e) {
                                    calculateShoppingList(root, result.shoppinglist);
                                });
                            $('#input-number-' + item.Id).on('input',
                                function (e) {
                                    calculateShoppingList(root, result.shoppinglist);
                                });
                        }
                    }

                    $('#shopping-list').append(
                        '<tr data-item-id="' +
                        root.Item.Id +
                        '"><td>' +
                        htmlName(root.Item) +
                        '</td><td>' +
                        htmlRarity(root.Item) +
                        '</td><td>' +
                        '' +
                        '</td><td>' +
                        htmlPriceSum(toPrice(0), 'sell', root.Item.Id) +
                        '</td><td>' +
                        '' +
                        '</td><td>' +
                        htmlPriceSum(toPrice(0), 'buy', root.Item.Id) +
                        '</td></tr>');

                    calculateShoppingList(root, result.shoppinglist);
                }
            }
        });
    }
}

function calculateShoppingList(root, list) {
    var sumSell = 0;
    var sumBuy = 0;


    for (var key in list) {
        if (list.hasOwnProperty(key)) {
            var rec = list[key].item;
            var item = rec.Item;
            var number = parseInt($('#input-number-' + item.Id).val());
            var sell = parseFloat($('#input-sell-' + item.Id).val());
            var buy = parseFloat($('#input-buy-' + item.Id).val());

            sumSell += filterResourcePrice(item.Id, sell) * number;
            sumBuy += filterResourcePrice(item.Id, buy) * number;
        }
    }

    var sellPrice = (root.Item.SellPrice * 0.9) / 100.0;
    var buyPrice = (root.Item.BuyPrice * 0.9) / 100.0;

    var sellProfit = sellPrice - sumSell;
    var buyProfit = buyPrice - sumBuy;
    var sellBuyProfit = sellPrice - sumBuy;

    var sellClass = sellProfit > 0 ? 'sum-pos' : 'sum-neg';
    var buyClass = buyProfit > 0 ? 'sum-pos' : 'sum-neg';
    var sellBuyClass = sellBuyProfit > 0 ? 'sum-pos' : 'sum-neg';

    $('#sum-sell-' + root.Item.Id).text(toFixed(-sumSell));
    $('#sum-buy-' + root.Item.Id).text(toFixed(-sumBuy));

    $('#sum-fee-sell-' + root.Item.Id).text(toFixed(sellPrice));
    $('#sum-fee-buy-' + root.Item.Id).text(toFixed(buyPrice));

    $('#sum-diff-sell-' + root.Item.Id).removeClass('sum-neg').removeClass('sum-pos').addClass(sellClass).
        text(toFixed(sellProfit));
    $('#sum-diff-buy-' + root.Item.Id).removeClass('sum-neg').removeClass('sum-pos').addClass(buyClass).
        text(toFixed(buyProfit));

    $('#sum-sell-buy-diff-' + root.Item.Id).removeClass('sum-neg').removeClass('sum-pos').addClass(sellBuyClass).
        text(toFixed(sellBuyProfit));
}

var ResourceNumbers =
{
    43: true, //Copper x100
    53: true, //Scrap x100
    85: true, //Wires x100
    119: true, //Coupons x100
    168: true, //Electronics x100
    330: true, //Taler x100
    337: true //Uran x100
};

function filterResourcePrice(id, value) {
    if (id in ResourceNumbers) {
        return value / 100.0;
    }
    return value;
}

// Ugh...
function htmlName(item) {
    return '<div class="clearfix content-heading">' +
        '<div class="clearfix vertical-center pull-left">' +
        '<div>' +
        '<a href="/item/' +
        item.Id +
        '">' +
        '<img style="margin-right: 8px; height: 32px;" src="/img/items/' +
        item.Image +
        '" /></a>' +
        '</div>' +
        '</div>' +
        '<a href="/item/' +
        item.Id +
        '" style="font-weight: bold;">' +
        item.Name +
        '</a>' +
        '<div style="font-size: 11px; font-weight: bold;">' +
        item.TypeName +
        '</div>' +
        '</div>';
}

function htmlRarity(item) {
    return '<span class="label label-' + item.RarityName + '">' + item.RarityName + '</span>';
}

function htmlNumber(value) {
    return '<div class="label-md rec-right">' + value + '</div>';
}

function htmlPriceSum(value, side, id) {
    var r = '<div class="label-md pull-left">' +
        'Price -10 %' +
        '</div>' +
        '<div class="recipe-price label-md rec-right">' +
        '<div class="text-right sum-value" id="sum-fee-' +
        side +
        '-' +
        id +
        '">' +
        '</div>' +
        '<img height="14" src="/img/Coin.png" />' +
        '</div>' +
        '<div class="label-md pull-left">' +
        'Cost' +
        '</div>' +
        '<div class="recipe-price label-md rec-right">' +
        '<div class="text-right sum-value" id="sum-' +
        side +
        '-' +
        id +
        '">' +
        '</div>' +
        '<img height="14" src="/img/Coin.png" />' +
        '</div>' +
        '<div class="label-md pull-left">' +
        'Profit' +
        '</div>' +
        '<div class="recipe-price label-md rec-right">' +
        '<div class="text-right sum-value" id="sum-diff-' +
        side +
        '-' +
        id +
        '">' +
        '</div>' +
        '<img height="14" src="/img/Coin.png" />' +
        '</div>';
    if (side === 'sell') {
        r += '<div class="label-md pull-left">' +
            'Sell-Buy Profit' +
            '</div>' +
            '<div class="recipe-price label-md rec-right">' +
            '<div class="text-right sum-value" id="sum-sell-buy-diff-' +
            id +
            '">' +
            '</div>' +
            '<img height="14" src="/img/Coin.png" />' +
            '</div>';
    }

    return r;
}

function htmlNumberInput(value, id) {
    return '<div class="recipe-price label-md rec-right"><input class="text-right" id="' +
        id +
        '" type="text" value="' +
        value +
        '"></div>';
}

function htmlPriceInput(value, id) {
    return '<div class="recipe-price label-md rec-right"><input class="text-right" id="' +
        id +
        '" type="text" value="' +
        value +
        '"><img height="14" src="/img/Coin.png" /></div>';
}

// Maybe someone can make this easier
function updateSum(root, item, result, recipe) {
    var valueSet = false;
    var foundItem = null;
    for (var i = 0; i < item.Ingredients.length; i++) {
        updateSum(root, item.Ingredients[i], result, recipe);
        var subItem = item.Ingredients[i];
        if (!result.map.hasOwnProperty(subItem.UniqueId)) {
            if ($('#uniqueid-' + subItem.UniqueId).is(":visible")) {
                if (!subItem.IsSumRow) {
                    result.items.push({
                        //sell: subItem.Item.SellPrice * Math.max(1, subItem.RootNumber),
                        //buy: subItem.Item.BuyPrice * Math.max(1, subItem.RootNumber),
                        item: subItem
                    });
                    if (result.shoppinglist.hasOwnProperty(subItem.Item.Id)) {
                        result.shoppinglist[subItem.Item.Id].number += subItem.RootNumber;
                    } else {
                        result.shoppinglist[subItem.Item.Id] = { number: subItem.RootNumber, item: subItem }
                    }
                    valueSet = true;
                    foundItem = subItem;
                }
            }
        }
    }
    if (valueSet && foundItem != null) {
        result.map[foundItem.ParentUniqueId] = true;
    }
}

function findSumItem(item, uniqueid) {
    if (item.IngredientSum !== null && item.IngredientSum.UniqueId === uniqueid) {
        return item;
    }

    for (var i = 0; i < item.Ingredients.length; i++) {
        var rs = findSumItem(item.Ingredients[i], uniqueid);
        if (rs) return rs;
    }
    return null;
}