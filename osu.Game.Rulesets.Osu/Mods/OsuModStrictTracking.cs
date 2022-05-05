// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading;
using osu.Framework.Graphics.Sprites;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Judgements;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Osu.Mods
{
    public class OsuModStrictTracking : Mod, IApplicableAfterBeatmapConversion, IApplicableToDrawableHitObject, IApplicableToDrawableRuleset<OsuHitObject>
    {
        public override string Name => @"Strict Tracking";
        public override string Acronym => @"ST";
        public override IconUsage? Icon => FontAwesome.Solid.PenFancy;
        public override ModType Type => ModType.DifficultyIncrease;
        public override string Description => @"Follow circles just got serious...";
        public override double ScoreMultiplier => 1.0;
        public override Type[] IncompatibleMods => new[] { typeof(ModClassic), typeof(OsuModTarget) };

        public void ApplyToDrawableHitObject(DrawableHitObject drawable)
        {
            if (drawable is DrawableSlider slider)
            {
                slider.Tracking.ValueChanged += e =>
                {
                    if (e.NewValue || slider.Judged) return;

                    var tail = slider.NestedHitObjects.OfType<StrictTrackingDrawableSliderTail>().First();

                    if (!tail.Judged)
                        tail.MissForcefully();
                };
            }
        }

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var osuBeatmap = (OsuBeatmap)beatmap;

            if (osuBeatmap.HitObjects.Count == 0) return;

            var hitObjects = osuBeatmap.HitObjects.Select(ho =>
            {
                if (ho is Slider slider)
                {
                    var newSlider = new StrictTrackingSlider(slider);
                    return newSlider;
                }

                return ho;
            }).ToList();

            osuBeatmap.HitObjects = hitObjects;
        }

        public void ApplyToDrawableRuleset(DrawableRuleset<OsuHitObject> drawableRuleset)
        {
            drawableRuleset.Playfield.RegisterPool<StrictTrackingSliderTailCircle, StrictTrackingDrawableSliderTail>(10, 100);
        }

        private class StrictTrackingSliderTailCircle : SliderTailCircle
        {
            public StrictTrackingSliderTailCircle(Slider slider)
                : base(slider)
            {
            }

            public override Judgement CreateJudgement() => new OsuJudgement();
        }

        private class StrictTrackingDrawableSliderTail : DrawableSliderTail
        {
            public override bool DisplayResult => true;
        }

        private class StrictTrackingSlider : Slider
        {
            public StrictTrackingSlider(Slider original)
            {
                StartTime = original.StartTime;
                Samples = original.Samples;
                Path = original.Path;
                NodeSamples = original.NodeSamples;
                RepeatCount = original.RepeatCount;
                Position = original.Position;
                NewCombo = original.NewCombo;
                ComboOffset = original.ComboOffset;
                LegacyLastTickOffset = original.LegacyLastTickOffset;
                TickDistanceMultiplier = original.TickDistanceMultiplier;
            }

            protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
            {
                var sliderEvents = SliderEventGenerator.Generate(StartTime, SpanDuration, Velocity, TickDistance, Path.Distance, this.SpanCount(), LegacyLastTickOffset, cancellationToken);

                foreach (var e in sliderEvents)
                {
                    switch (e.Type)
                    {
                        case SliderEventType.Tick:
                            AddNested(new SliderTick
                            {
                                SpanIndex = e.SpanIndex,
                                SpanStartTime = e.SpanStartTime,
                                StartTime = e.Time,
                                Position = Position + Path.PositionAt(e.PathProgress),
                                StackHeight = StackHeight,
                                Scale = Scale,
                            });
                            break;

                        case SliderEventType.Head:
                            AddNested(HeadCircle = new SliderHeadCircle
                            {
                                StartTime = e.Time,
                                Position = Position,
                                StackHeight = StackHeight,
                            });
                            break;

                        case SliderEventType.LegacyLastTick:
                            AddNested(TailCircle = new StrictTrackingSliderTailCircle(this)
                            {
                                RepeatIndex = e.SpanIndex,
                                StartTime = e.Time,
                                Position = EndPosition,
                                StackHeight = StackHeight
                            });
                            break;

                        case SliderEventType.Repeat:
                            AddNested(new SliderRepeat(this)
                            {
                                RepeatIndex = e.SpanIndex,
                                StartTime = StartTime + (e.SpanIndex + 1) * SpanDuration,
                                Position = Position + Path.PositionAt(e.PathProgress),
                                StackHeight = StackHeight,
                                Scale = Scale,
                            });
                            break;
                    }
                }

                UpdateNestedSamples();
            }
        }
    }
}
