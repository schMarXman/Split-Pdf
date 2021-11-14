using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Split
{
    public class MathExpressionParser
    {
        public Func<float, float> OnParseSucceeded;

        public bool Parse(string expression, out float result)
        {
            result = -1;
            float a, b;
            string op;

            if (!SplitExpression(expression, out a, out b, out op))
            {
                return false;
            }

            switch (op)
            {
                case "+":
                    result = a + b;
                    break;
                case "-":
                    result = a - b;
                    break;
                case "/":
                    result = a / b;
                    break;
                case "*":
                    result = a * b;
                    break;
                default:
                    throw new Exception("Unknown operator!");
            }

            if (OnParseSucceeded != null)
            {
                result = OnParseSucceeded.Invoke(result);
            }

            return true;
        }

        private bool SplitExpression(string expression, out float a, out float b, out string op)
        {
            List<string> operators = new List<string> { "+", "-", "/", "*" };

            a = 0;
            b = 0;
            op = "";

            string[] splitExpression = new string[0];
            foreach (var o in operators)
            {
                splitExpression = expression.Split(o.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                if (splitExpression.Length > 1)
                {
                    op = o;
                    break;
                }
            }
            if (splitExpression.Length != 2)
            {
                return false;
            }

            if (!float.TryParse(splitExpression[0].Trim(), out a))
            {
                return false;
            }

            if (!float.TryParse(splitExpression[1].Trim(), out b))
            {
                return false;
            }

            return true;
        }
    }
}
