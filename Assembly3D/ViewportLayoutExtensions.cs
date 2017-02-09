﻿using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using devDept.Eyeshot;
using devDept.Eyeshot.Entities;

namespace Weingartner.Eyeshot.Assembly3D
{
    public static class ViewportLayoutExtensions
    {
        public static void InvalidateAndRegen(this ViewportLayout vpl, bool withRegen=true)
        {
            if(withRegen)
                RegenAll(vpl);
            vpl.Invalidate();
        }

        /// <summary>
        /// Attempts to regenerate the viewportLayout so that all entities are
        /// correctly regenerated.
        /// </summary>
        /// <param name="vpl"></param>
        public static void RegenAll(this ViewportLayout vpl)
        {
            if (vpl.renderContext == null)
                return;

            // As of Eyeshot v9.0.218, `EntityList.RegenAllCurved()` triggers
            // a recomputation of the bounding box of all entities,
            // updates `EntityList.VisualRefinementTolerance`
            // and then calls `EntityList.RegenAllCurved(EntityList.VisualRefinementTolerance)`.
            // But because `EntityList.Regen()` already estimates the bounding box
            // and updates `EntityList.VisualRefinementTolerance` we can directly call
            // `EntityList.RegenAllCurved(EntityList.VisualRefinementTolerance)`.
            vpl.Entities.Regen();

            //on first load the VisualRefinementTolerance seems to be to high, which
            //caused serious polygon effect, therefore I added this hack...
            var tol = vpl.Entities.VisualRefinementTolerance > 1e-4
                          ? 1e-5
                          : vpl.Entities.VisualRefinementTolerance;

            vpl.Entities.RegenAllCurved(tol);
            vpl.Labels.Regen();
        }

        /// <summary>
        /// Validate the all the block reference entities refer to real blocks
        /// in the viewport layout
        /// </summary>
        /// <param name="viewportLayout"></param>
        public static void ValidateViewportLayout(this ViewportLayout viewportLayout)
        {
            var entityList = viewportLayout.Entities;
            viewportLayout.ValidateViewportEntities(entityList);
            foreach (var block in viewportLayout.Blocks)
            {
                viewportLayout.ValidateViewportEntities(block.Value.Entities);
            }
        }

        private static void ValidateViewportEntities(this ViewportLayout viewportLayout, IEnumerable<Entity> entityList)
        {
            foreach (var entity in entityList)
            {
                var blockRef = entity as BlockReference;
                if (blockRef != null)
                {
                    if (!viewportLayout.Blocks.ContainsKey(blockRef.BlockName))
                        throw new Exception($"Block {blockRef.BlockName} not found in viewport layout");

                    var block = viewportLayout.Blocks[blockRef.BlockName];

                    ValidateViewportEntities(viewportLayout, block.Entities);
                }
            }
        }

        /// <summary>
        /// Add a block to the viewportLayout and return a disposable that when
        /// disposed will remove the block
        /// </summary>
        /// <param name="vpl"></param>
        /// <param name="key"></param>
        /// <param name="block"></param>
        /// <returns></returns>
        public static IDisposable AddBlock(this ViewportLayout vpl, string key, Block block)
        {
            vpl.Blocks.Add(key, block);
            return Disposable.Create(() => vpl.Blocks.Remove(key));
        }

        public static IDisposable AddEntity(this ViewportLayout vpl, Entity entity)
        {
            vpl.Entities.Add(entity);
            return Disposable.Create(() => vpl.Entities.Remove(entity));
        }

        public static IDisposable SetCurrent(this ViewportLayout vpl, BlockReference blkRef)
        {
            vpl.Entities.SetCurrent(blkRef);
            return Disposable.Create( () => vpl.Entities.SetCurrent(null));
        }

        public static IObservable<ViewportLayout.SelectionChangedEventArgs> SelectionChangedObservable(this ViewportLayout viewport)
        {
            return Observable.FromEventPattern<ViewportLayout.SelectionChangedEventHandler, ViewportLayout.SelectionChangedEventArgs>
                ( h=>viewport.SelectionChanged+=h
                  , h=>viewport.SelectionChanged-=h
                )
                .Select(e=>e.EventArgs);
        }

        public static IDisposable BindToViewport(this ViewportLayout viewportLayout, IObservable<Assembly3D> assemblyObserver)
        {
            return new AssemblyEyeshotViewportAdapter(viewportLayout)
                .BindAssemblyToViewport (assemblyObserver, () => { });
        }
        public static IDisposable BindToViewport(this ViewportLayout viewportLayout, Assembly3D assembly)
        {
            return BindToViewport(viewportLayout, Observable.Return(assembly));
        }
    }

}
