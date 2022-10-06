using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;
using HarmonyLib;
using CM_Callouts;
using Verse.Grammar;
using CM_Callouts.PendingCallouts;
using Verse.AI;
using Verse.Grammar;

namespace CalloutsExpanded
{
	public class CalloutConstantByPawnkindDef : Def
	{
		public List<PawnKindDef> pawnKindDefs = new List<PawnKindDef>();
		public string name;
		public string value;
	}
	public class CalloutConstantByThingDef : Def
	{
		public List<ThingDef> thingDefs = new List<ThingDef>();
		public string name;
		public string value;
	}
	public class CalloutConstantByHediffDef : Def
	{
		public List<HediffDef> hediffDefs = new List<HediffDef>();
		public string name;
		public string value;
	}
	public class CalloutConstantByHediffStage : Def
	{
		public List<HediffAndStage> hediffsAndStages = new List<HediffAndStage> ();
		public string name;
		public string value;
	}
	public class HediffAndStage
	{
		public HediffDef hediffDef;
		public int stage;
	}
	public class CalloutConstantByNeedDef : Def
	{
		public NeedDef needDef;
		public float needLevel;
		public bool aboveLevel;
		public string name;
		public string value;
	}

	public class PendingCalloutEventTradeInteraction : PendingCalloutEventDoublePawn
	{
		public PendingCalloutEventTradeInteraction(Pawn _initiator, Pawn _recipient, RulePackDef _initiatorRulePack, RulePackDef _recipientRulePack) : base(CalloutCategory.Undefined, _initiator, _recipient, _initiatorRulePack, _recipientRulePack)
		{
		}
		protected override GrammarRequest PrepareGrammarRequest(RulePackDef rulePack)
		{
			GrammarRequest result = base.PrepareGrammarRequest(rulePack);
			result.Constants.Add("RECIPIENT_TraderKindDef", recipient.TraderKind.defName);
			return result;
		}
	}
	
	[DefOf]
	public static class CalloutsExpandedDefOf
    {
		public static RulePackDef CM_Callouts_RulePack_Trade_Initiated;
		public static RulePackDef CM_Callouts_RulePack_Trade_Received;
	}

	[StaticConstructorOnStartup]
	public static class CallOutsExpanded
	{
		static CallOutsExpanded()
		{
			Harmony harmony = new Harmony("bratwurstinator.CalloutsExpanded");
			harmony.PatchAll();
		}
	}
	[HarmonyPatch(typeof(CalloutTracker), "RequestCallout")]
	public static class RequestCalloutPatch
	{
		public static bool HasHediffOfStage(Pawn pawn, HediffAndStage hdas)
		{
			HediffSet hs = pawn.health.hediffSet;
			for (int i = 0; i < hs.hediffs.Count; i++)
			{
				if (hs.hediffs[i].def == hdas.hediffDef && hs.hediffs[i].CurStageIndex == hdas.stage)
				{
					return true;
				}
			}
			return false;
		}
		public static void Prefix(Pawn pawn, RulePackDef rulePack, GrammarRequest grammarRequest, CalloutTracker __instance)
		{
            if (!__instance.CanCalloutNow(pawn) || pawn is null)
            {
				return;
            }
			foreach (CalloutConstantByPawnkindDef calloutConstantByPawnkindDef in DefDatabase<CalloutConstantByPawnkindDef>.AllDefs)
			{
				if (calloutConstantByPawnkindDef.pawnKindDefs.Any((PawnKindDef pkd) => pkd.defName == pawn.kindDef.defName)) 
                {
					grammarRequest.Constants[calloutConstantByPawnkindDef.name] = calloutConstantByPawnkindDef.value;
				}
			}
			foreach (CalloutConstantByThingDef calloutConstantByThingDef in DefDatabase<CalloutConstantByThingDef>.AllDefs)
			{
				if (calloutConstantByThingDef.thingDefs.Any((ThingDef td) => td.defName == pawn.def.defName))
				{
					grammarRequest.Constants[calloutConstantByThingDef.name] = calloutConstantByThingDef.value;
				}
			}
			if (pawn.health != null && pawn.health.hediffSet != null)
			{
				foreach (CalloutConstantByHediffDef calloutConstantByHediffDef in DefDatabase<CalloutConstantByHediffDef>.AllDefs)
				{
					if (calloutConstantByHediffDef.hediffDefs.Any((HediffDef hd) => pawn.health.hediffSet.HasHediff(hd)))
					{
						grammarRequest.Constants[calloutConstantByHediffDef.name] = calloutConstantByHediffDef.value;
					}
				}
				foreach (CalloutConstantByHediffStage calloutConstantByHediffStage in DefDatabase<CalloutConstantByHediffStage>.AllDefs)
				{
					if (calloutConstantByHediffStage.hediffsAndStages.Any((HediffAndStage hdas) => HasHediffOfStage(pawn, hdas)))
					{
						grammarRequest.Constants[calloutConstantByHediffStage.name] = calloutConstantByHediffStage.value;
					}
				}
			}
			if (pawn.needs != null)
			{
				foreach (CalloutConstantByNeedDef calloutConstantByNeedDef in DefDatabase<CalloutConstantByNeedDef>.AllDefs)
				{
					Need need = pawn.needs.TryGetNeed(calloutConstantByNeedDef.needDef);
					if (need != null) 
					{
						bool above = need.CurLevel > calloutConstantByNeedDef.needLevel;
						if (calloutConstantByNeedDef.aboveLevel ? above : !above)
						{
							grammarRequest.Constants[calloutConstantByNeedDef.name] = calloutConstantByNeedDef.value;
						}
					}
				}
			}
			return;
		}
	}

	[HarmonyPatch(typeof(JobDriver_TradeWithPawn), "MakeNewToils")]
	public static class TradeWithPawn_CalloutsExpanded_Patch
    {
		public static IEnumerable<Toil> Postfix(IEnumerable<Toil> values, JobDriver_TradeWithPawn __instance)
        {
			List<Toil> toils = values.ToList();
			int i;
			for(i = 0; i < toils.Count; i++)
            {
				if(i == toils.Count-1)
                {
					Toil toil = toils[i];
					Action action = toil.initAction;
					toil.initAction = delegate () 
					{
						action();
						if(toil.actor == null || __instance.Trader == null)
                        {
							return;
                        }
						new PendingCalloutEventTradeInteraction(toil.actor, __instance.Trader, //set the Trader property to public with an assembly editor
							CalloutsExpandedDefOf.CM_Callouts_RulePack_Trade_Initiated,
							CalloutsExpandedDefOf.CM_Callouts_RulePack_Trade_Received).AttemptCallout();
					};
					yield return toils[i];
					continue;
                }
				yield return toils[i];
            }
        }
    }
}