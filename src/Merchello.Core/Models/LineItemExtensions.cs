﻿using System.Collections.Generic;
using System.Linq;
using Merchello.Core.Gateways.Shipping;
using Merchello.Core.Gateways.Taxation;
using Merchello.Core.Models.TypeFields;
using Umbraco.Core.Logging;

namespace Merchello.Core.Models
{
    /// <summary>
    /// Extension methods for <see cref="ILineItem"/>
    /// </summary>
    public static class LineItemExtensions
    {

        #region LineItemContainer
        
        /// <summary>
        /// Adds a <see cref="IProductVariant"/> line item to the collection
        /// </summary>
        public static void AddItem(this ILineItemContainer container, IProductVariant productVariant, int quantity)
        {
            var extendedData = new ExtendedDataCollection();
            
            container.AddItem(productVariant, quantity, extendedData);
        }


        /// <summary>
        /// Adds a <see cref="IProductVariant"/> line item to the collection
        /// </summary>
        public static void AddItem(this ILineItemContainer container, IProductVariant productVariant, int quantity, ExtendedDataCollection extendedData)
        {
            extendedData.AddProductVariantValues(productVariant);
            container.AddItem(LineItemType.Product, productVariant.Name, productVariant.Sku, quantity, productVariant.Price, extendedData);
        }


        /// <summary>
        /// Adds a line item to the collection
        /// </summary>
        public static void AddItem(this ILineItemContainer container, LineItemType lineItemType, string name, string sku, int quantity, decimal amount)
        {
            container.AddItem(lineItemType, name, sku, quantity, amount, new ExtendedDataCollection());
        }

        /// <summary>
        /// Adds a line item to the collection
        /// </summary>
        public static void AddItem(this ILineItemContainer container, LineItemType lineItemType, string name, string sku, int quantity, decimal amount, ExtendedDataCollection extendedData)
        {
            var lineItem = new ItemCacheLineItem(lineItemType, name, sku, quantity, amount, extendedData)
                {
                    ContainerKey = container.Key
                };
            
            container.AddItem(lineItem);
        }

        /// <summary>
        /// Adds a line item to the collection
        /// </summary>
        public static void AddItem(this ILineItemContainer container, ILineItem lineItem)
        {
            container.Items.Add(lineItem);
        }
       
        #endregion

        /// <summary>
        /// Converts a line item of one type to a line item of another type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="lineItem"></param>
        /// <returns>A <see cref="LineItemBase"/> of type T</returns>
        public static T AsLineItemOf<T>(this ILineItem lineItem) where T : class, ILineItem
        {    
            var ctrValues = new object[]
                {                    
                    lineItem.LineItemTfKey,
                    lineItem.Name,
                    lineItem.Sku,
                    lineItem.Quantity,
                    lineItem.Price,
                    lineItem.ExtendedData
                };


            var attempt = ActivatorHelper.CreateInstance<LineItemBase>(typeof(T).FullName, ctrValues);
            if (!attempt.Success)
            {
                LogHelper.Error<ILineItem>("Failed to convertion ILineItem", attempt.Exception);
                throw attempt.Exception;
            }
            attempt.Result.Exported = lineItem.Exported;

            return attempt.Result as T;
        }


        /// <summary>
        /// Creates a line item of a particular type for a shipment rate quote
        /// </summary>
        /// <typeparam name="T">The type of the line item to create</typeparam>
        /// <param name="shipmentRateQuote">The <see cref="ShipmentRateQuote"/> to be translated to a line item</param>
        /// <returns>A <see cref="LineItemBase"/> of type T</returns>
        public static T AsLineItemOf<T>(this IShipmentRateQuote shipmentRateQuote) where T : LineItemBase
        {
            var extendedData = new ExtendedDataCollection();
            extendedData.AddShipment(shipmentRateQuote.Shipment);
            
            var ctrValues = new object[]
                {
                    EnumTypeFieldConverter.LineItemType.Shipping.TypeKey,
                    shipmentRateQuote.ShimpentLineItemName(),
                    shipmentRateQuote.ShipMethod.ServiceCode, // TODO this may not be unique (SKU) once multiple shipments are exposed
                    1,
                    shipmentRateQuote.Rate,
                    extendedData
                };

            var attempt = ActivatorHelper.CreateInstance<LineItemBase>(typeof (T).FullName, ctrValues);

            if (!attempt.Success)
            {
                LogHelper.Error<ILineItem>("Failed instiating a line item from shipmentRateQuote", attempt.Exception);
                throw attempt.Exception;
            }
            return attempt.Result as T;
        }

        /// <summary>
        /// Creates a line item of a particular type for a invoiceTaxResult
        /// </summary>
        /// <typeparam name="T">The type of the line item to be created</typeparam>
        /// <param name="taxCalculationResult">The <see cref="ITaxCalculationResult"/> to be converted to a line item</param>
        /// <returns>A <see cref="ILineItem"/> representing the <see cref="ITaxCalculationResult"/></returns>
        public static T AsLineItemOf<T>(this ITaxCalculationResult taxCalculationResult) where T : LineItemBase
        {
            var ctrValues = new object[]
            {
                EnumTypeFieldConverter.LineItemType.Tax.TypeKey,
                taxCalculationResult.Name,
                "Tax", // TODO this may not e unqiue (SKU),
                1,
                taxCalculationResult.TaxAmount,
                taxCalculationResult.ExtendedData
            };

            var attempt = ActivatorHelper.CreateInstance<LineItemBase>(typeof (T).FullName, ctrValues);

            if (!attempt.Success)
            {
                LogHelper.Error<ILineItem>("Failed instiating a line item from invoiceTaxResult", attempt.Exception);
                throw attempt.Exception;
            }

            return attempt.Result as T;
        }


        /// <summary>
        /// Returns a collection of shippable line items
        /// </summary>
        /// <param name="container">The <see cref="ILineItemContainer"/></param>
        /// <returns>A collection of line items that can be shipped</returns>
        public static IEnumerable<ILineItem> ShippableItems(this ILineItemContainer container)
        {
            return container.Items.Where(x => x.IsShippable());
        }

        /// <summary>
        /// True/false indicating whether or not this lineItem represents a line item that can be shipped (eg. a product)
        /// </summary>
        /// <param name="lineItem">The <see cref="ILineItem"/></param>
        public static bool IsShippable(this ILineItem lineItem)
        {
            return lineItem.LineItemType == LineItemType.Product &&
                   lineItem.ExtendedData.ContainsProductVariantKey() &&
                   lineItem.ExtendedData.GetShippableValue() &&
                   lineItem.ExtendedData.ContainsWarehouseCatalogKey();
        }
    }
}

