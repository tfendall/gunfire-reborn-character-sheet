using System.Text.RegularExpressions;

// Parses description placeholders only. Arithmetic remains in the game evaluator.
internal static class EffectExpression
{
    // Preflight the entire template before any native calls, including later placeholders.
    internal static void Validate(string description,IReadOnlyDictionary<string,long> inputs)
    {
        if(description.IndexOfAny(new[]{'{','}','[',']'})>=0)
            throw new InvalidOperationException("unsupported-template-markup");
        Render(description,inputs,_=>"0");
    }
    internal static int[] Batch(IEnumerable<int> ids,int after,int limit=8)
        => ids.Distinct().OrderBy(id=>id<=after?1:0).ThenBy(id=>id).Take(limit).ToArray();
    internal static string Render(string description, IReadOnlyDictionary<string,long> inputs, Func<string,string> evaluate)
    {
        if(description.Length>2048)throw new InvalidOperationException("description-limit");
        int calls=0;
        string Expand(string text,int depth)
        {
            if(depth>8)throw new InvalidOperationException("expression-depth");
            var output=new System.Text.StringBuilder();
            for(int i=0;i<text.Length;i++)
            {
                if(text[i]==')')throw new InvalidOperationException("unbalanced-expression");
                if(text[i]!='('){output.Append(text[i]);continue;}
                int start=i,level=1;
                while(++i<text.Length && level>0)
                {if(text[i]=='(')level++;else if(text[i]==')')level--;}
                if(level!=0)throw new InvalidOperationException("unbalanced-expression");
                // i is one past the closing parenthesis.
                string inner=Expand(text.Substring(start+1,i-start-2),depth+1);
                foreach(Match token in Regex.Matches(inner,@"[A-Za-z_][A-Za-z_0-9]*"))
                    if(!inputs.ContainsKey(token.Value))throw new InvalidOperationException("missing-variable:"+token.Value);
                if(!Regex.IsMatch(inner,@"^[A-Za-z_0-9+*/%.\s\-]+$"))throw new InvalidOperationException("unsupported-expression");
                if(++calls>16)throw new InvalidOperationException("expression-count");
                string result=evaluate("("+inner+")");
                if(!double.TryParse(result,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out double number) || !double.IsFinite(number))
                    throw new InvalidOperationException("non-numeric-result");
                output.Append(result);i--;
            }
            return output.ToString();
        }
        return Expand(description,0);
    }
}
