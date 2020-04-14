using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace Reflection.Differentiation
{
    public static class Algebra
    {
        private static Dictionary<ExpressionType, Func<Expression, Expression>> differentiatedFuncs;
        private static Dictionary<string, Func<MethodCallExpression, Expression>> functionNamesAndTheirMeaning;

        public static Expression<Func<double, double>> Differentiate(Expression<Func<double, double>> function)
        {
            return Expression.Lambda<Func<double, double>>(differentiatedFuncs[function.Body.NodeType](function.Body), function.Parameters);
        }

        static Algebra()
        {
            functionNamesAndTheirMeaning = new Dictionary<string, Func<MethodCallExpression, Expression>>
            {
                ["Sin"] = e => DiffSin(e),
                ["Cos"] = e => DifCos(e),
                ["Pow"] = e => DiffPow(e),
                ["Exp"] = e => DiffExp(e),
                ["Log"] = e => DiffLog(e),
                ["Tan"] = e => DiffTan(e),
                ["Asin"] = e => DiffAsin(e),
                ["Acos"] = e => DiffAcos(e),
                ["Atan"] = e => DiffAtan(e)
            };
            differentiatedFuncs = new Dictionary<ExpressionType, Func<Expression, Expression>>();
            differentiatedFuncs.Add(ExpressionType.Constant, expression => Expression.Constant(0.0));
            differentiatedFuncs.Add(ExpressionType.Parameter, expression => Expression.Constant(1.0));
            differentiatedFuncs.Add(ExpressionType.Add, expression =>
            {
                var e = (BinaryExpression)expression;
                return Expression.Add(differentiatedFuncs[e.Left.NodeType](e.Left), differentiatedFuncs[e.Right.NodeType](e.Right));
            });
            differentiatedFuncs.Add(ExpressionType.Multiply, expression =>
            {
                var e = (BinaryExpression)expression;
                return Expression.Add(Expression.Multiply(e.Left, differentiatedFuncs[e.Right.NodeType](e.Right)),
                                      Expression.Multiply(e.Right, differentiatedFuncs[e.Left.NodeType](e.Left)));
            });
            differentiatedFuncs.Add(ExpressionType.Divide, expression =>
            {
                var e = (BinaryExpression)expression;
                return Expression.Divide
                    (
                      Expression.Add(Expression.Multiply(e.Right, differentiatedFuncs[e.Left.NodeType](e.Left)),
                                     Expression.Multiply(Expression.Multiply(e.Left, Expression.Constant(-1.0)),
                                                      differentiatedFuncs[e.Right.NodeType](e.Right))),
                      Expression.Multiply(e.Right, e.Right)
                    );
            });
            differentiatedFuncs.Add(ExpressionType.Call, expression =>
            {
                var e = (MethodCallExpression)expression;
                if (functionNamesAndTheirMeaning.ContainsKey(e.Method.Name))
                    return functionNamesAndTheirMeaning[e.Method.Name](e);
                return null;
            });
        }

        static Expression DiffSin(MethodCallExpression e)
        {
            return Expression.Multiply(
                        Expression.Call(null, typeof(Math).GetMethod("Cos"), e.Arguments[0]),
                        differentiatedFuncs[e.Arguments[0].NodeType](e.Arguments[0]));
        }
        static Expression DifCos(MethodCallExpression e)
        {
            return Expression.Multiply(
                       Expression.Multiply(Expression.Call(null, typeof(Math).GetMethod("Sin"), e.Arguments[0]),
                       Expression.Constant(-1.0)),
                       differentiatedFuncs[e.Arguments[0].NodeType](e.Arguments[0]));
        }
        static Expression DiffPow(MethodCallExpression e)
        {
            if (e.Arguments[0] is ParameterExpression && e.Arguments[1] is ConstantExpression)
                return Expression.Multiply(Expression.Multiply(e.Arguments[1],
                                                                    Expression.Call(null, typeof(Math).GetMethod("Pow"),
                                                                                                        e.Arguments[0],
                                                                                                        Expression.Add(e.Arguments[1],
                                                                                                        Expression.Constant(-1.0)))),
                                                 differentiatedFuncs[e.Arguments[0].NodeType](e.Arguments[0]));
            else if (e.Arguments[0] is ConstantExpression && e.Arguments[1] is ParameterExpression)
            {
                var internalFunc = differentiatedFuncs[e.Arguments[1].NodeType](e.Arguments[1]);
                var externalFunc = Expression.Multiply(e, Expression.Call(null, typeof(Math).GetMethod("Log", new[] { typeof(double) }), e.Arguments[0]));
                return Expression.Multiply(internalFunc, externalFunc);
            }
            else
            {
                var result = Expression.Call(null, typeof(Math).GetMethod("Exp"),
                                 Expression.Multiply(e.Arguments[1], Expression.Call(null, typeof(Math).GetMethod("Log", new[] { typeof(double) }), e.Arguments[0])));
                return differentiatedFuncs[result.NodeType](result);
            }
        }
        static Expression DiffExp(MethodCallExpression e)
        {
            return Expression.Multiply(Expression.Call(null, typeof(Math).GetMethod("Exp"), e.Arguments[0]),
                                                    differentiatedFuncs[e.Arguments[0].NodeType](e.Arguments[0]));
        }
        static Expression DiffLog(MethodCallExpression e)
        {
            if (e.Arguments.Count == 2)
                return Expression.Divide(differentiatedFuncs[e.Arguments[0].NodeType](e.Arguments[0]),
                   Expression.Multiply(e.Arguments[0], Expression.Call(null, typeof(Math).GetMethod("Log", new[] { typeof(double) }), e.Arguments[1]))
                   );
            else
                return Expression.Divide(differentiatedFuncs[e.Arguments[0].NodeType](e.Arguments[0]), e.Arguments[0]);
        }
        static Expression DiffTan(MethodCallExpression e)
        {
            var cos = Expression.Call(null, typeof(Math).GetMethod("Cos"), e.Arguments[0]);
            return Expression.Divide(differentiatedFuncs[e.Arguments[0].NodeType](e.Arguments[0]), Expression.Multiply(cos, cos));
        }
        static Expression DiffAsin(MethodCallExpression e)
        {
            return Expression.Multiply(
                Expression.Call(null, typeof(Math).GetMethod("Pow"), Expression.Add(Expression.Constant(1.0),
                                                                                    Expression.Multiply(Expression.Multiply(Expression.Constant(-1.0),
                                                                                                                            e.Arguments[0]),
                                                                                                       e.Arguments[0])),
                                Expression.Constant(-0.5)),
                differentiatedFuncs[e.Arguments[0].NodeType](e.Arguments[0]));
        }
        static Expression DiffAcos(MethodCallExpression e)
        {
            return Expression.Multiply(
               Expression.Call(null, typeof(Math).GetMethod("Pow"), Expression.Add(Expression.Constant(1.0),
                                                                                   Expression.Multiply(Expression.Multiply(Expression.Constant(-1.0),
                                                                                                                           e.Arguments[0]),
                                                                                                      e.Arguments[0])),
                               Expression.Constant(-0.5)),
               Expression.Multiply(differentiatedFuncs[e.Arguments[0].NodeType](e.Arguments[0]), Expression.Constant(-1.0)));
        }
        static Expression DiffAtan(MethodCallExpression e)
        {
            return Expression.Multiply(
                Expression.Divide(Expression.Constant(1.0), Expression.Add(Expression.Constant(1.0), Expression.Multiply(e.Arguments[0], e.Arguments[0]))),
                differentiatedFuncs[e.Arguments[0].NodeType](e.Arguments[0])
                );
        }
    }
}