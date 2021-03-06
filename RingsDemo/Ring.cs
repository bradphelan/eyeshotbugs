﻿using System;
using System.Collections.Generic;
using System.Drawing;
using devDept.Eyeshot.Entities;
using devDept.Geometry;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Weingartner.EyeShot.Assembly3D;

namespace RingsDemo
{
    public class Ring : Assembly3D
    {
        [Reactive] public bool IsSphere { get; set;}
        [Reactive] public bool Active { get; set;}

        public Ring()
        {
            this.WhenAnyValue(p => p.IsSphere)
                .ObserveOn(this)
                .Subscribe(v =>
                {
                    Clear();
                    Add(MakeRing(v));
                });

            this.WhenAnyValue(p => p.Active)
                .ObserveOn(this, false)
                .Subscribe
                (active =>
                {
                    foreach (var e in this.Block.Entities)
                    {
                        e.SetColor(active ? Color.Green : Color.Gray);
                    }
                });

        }
        
        private static IEnumerable<Entity> MakeRing(bool isSphere)
        {
            var d = 10;
            for (int i = 0; i < d; i++)
            {

                var mesh = isSphere
                               ? Solid.CreateSphere(8,20,20)
                               : Solid.CreateBox(8, 8, 8);
                mesh.SetColor(Color.Green);
                mesh.Translate(20, 0, 0);
                mesh.Rotate(Math.PI * 2 / d * i, Vector3D.AxisZ);
                yield return mesh;
            }
        }
    }
}