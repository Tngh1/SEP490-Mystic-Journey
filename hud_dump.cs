var hud=GameObject.Find("HUD");
if(hud==null){ foreach(var g in UnityEngine.Object.FindObjectsByType<GameObject>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None)) if(g.name=="HUD"){hud=g;break;} }  // Entity not found — short-circuit with appropriate error result
var sb=new System.Text.StringBuilder();
if(hud==null) return "HUD not found";  // Entity not found — short-circuit with appropriate error result
System.Action<Transform,int> walk=null;
walk=(t,d)=>{ if(d>4) return; var cn=""; foreach(var c in t.GetComponents<Component>()){ if(c!=null && !(c is RectTransform) && !(c is CanvasRenderer)) cn+=c.GetType().Name+","; } sb.AppendLine(new string(' ',d*2)+t.name+" ["+cn+"] active="+t.gameObject.activeSelf); foreach(Transform ch in t) walk(ch,d+1); };
walk(hud.transform,0);
return sb.ToString();
