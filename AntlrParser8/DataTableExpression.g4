grammar DataTableExpression;

// Parser rules (lowercase names)
expression
    : orExpression
    ;

orExpression
    : andExpression (OR andExpression)*
    ;

andExpression
    : notExpression (AND notExpression)*
    ;

notExpression
    : NOT? comparisonExpression
    ;
/*
comparisonExpression
    : additiveExpression (
        (EQUALS | NOT_EQUALS | LESS_THAN | LESS_THAN_OR_EQUAL | GREATER_THAN | GREATER_THAN_OR_EQUAL | LIKE) additiveExpression
        | IN inExpression
    )?
    ;
*/
comparisonExpression
    : additiveExpression (
        (EQUALS | NOT_EQUALS | LESS_THAN | LESS_THAN_OR_EQUAL | GREATER_THAN | GREATER_THAN_OR_EQUAL | LIKE) additiveExpression
        | IN inExpression
        | IS NULL
        | IS NOT NULL
    )?
    ;

inExpression
    : LPAREN (expr+=expression (COMMA expr+=expression)*) RPAREN #InList
    ;
    
additiveExpression
    : multiplicativeExpression ((PLUS | MINUS) multiplicativeExpression)*
    ;

multiplicativeExpression
    : unaryExpression ((MULTIPLY | DIVIDE | MODULO) unaryExpression)*
    ;

unaryExpression
    : (PLUS | MINUS)? primaryExpression
    ;

primaryExpression
    : LPAREN expression RPAREN
    | functionCall
    | columnReference
    | literal
    ;

functionCall
    : functionName LPAREN argumentList? RPAREN
    ;

functionName
    : CONVERT | LEN | ISNULL | IIF | TRIM | SUBSTRING
    | SUM | AVG | MIN | MAX | COUNT | STDEV | VAR
    ;

argumentList
    : expression (COMMA expression)*
    ;

columnReference
    : BRACKETED_IDENTIFIER
    | BACKTICK IDENTIFIER BACKTICK
    | IDENTIFIER
    | PARENT DOT IDENTIFIER
    | CHILD DOT IDENTIFIER
    | PARENT LPAREN IDENTIFIER RPAREN DOT IDENTIFIER
    | CHILD LPAREN IDENTIFIER RPAREN DOT IDENTIFIER
    ;

literal
    : STRING_LITERAL
    | INTEGER_LITERAL
    | DECIMAL_LITERAL
    | BOOLEAN_LITERAL
    | DATE_LITERAL
    | NULL_LITERAL
    ;

// Lexer rules (uppercase names)
// Operators
EQUALS              : '=' | '==' ;
NOT_EQUALS          : '<>' | '!=';
LESS_THAN           : '<' ;
LESS_THAN_OR_EQUAL  : '<=' ;
GREATER_THAN        : '>' ;
GREATER_THAN_OR_EQUAL : '>=' ;
PLUS                : '+' ;
MINUS               : '-' ;
MULTIPLY            : '*' ;
DIVIDE              : '/' ;
MODULO              : '%' ;

// Logical operators
AND                 : 'AND' | 'and' ;
OR                  : 'OR' | 'or' ;
NOT                 : 'NOT' | 'not' ;
LIKE                : 'LIKE' | 'like' ;
IN                  : 'IN' | 'in' ;

// Functions
CONVERT             : 'CONVERT' | 'convert' ;
LEN                 : 'LEN' | 'len' ;
ISNULL              : 'ISNULL' | 'isnull' ;
IIF                 : 'IIF' | 'iif' ;
TRIM                : 'TRIM' | 'trim' ;
SUBSTRING           : 'SUBSTRING' | 'substring' ;
SUM                 : 'SUM' | 'sum' ;
AVG                 : 'AVG' | 'avg' ;
MIN                 : 'MIN' | 'min' ;
MAX                 : 'MAX' | 'max' ;
COUNT               : 'COUNT' | 'count' ;
STDEV               : 'STDEV' | 'stdev' ;
VAR                 : 'VAR' | 'var' ;

// Keywords
PARENT              : 'PARENT' | 'parent' ;
CHILD               : 'CHILD' | 'child' ;
fragment TRUE       : 'TRUE' | 'true' ;
fragment FALSE      : 'FALSE' | 'false' ;
NULL                : 'NULL' | 'null' ;
IS                  : 'IS' | 'is' ;

// Punctuation
LPAREN              : '(' ;
RPAREN              : ')' ;
LBRACKET            : '[' ;
RBRACKET            : ']' ;
BACKTICK            : '`' ;
DOT                 : '.' ;
COMMA               : ',' ;
HASH                : '#' ;

// Literals
STRING_LITERAL      : '\'' ( ~'\'' | '\'\'' )* '\'' ; // Allow escaped single quotes
DATE_LITERAL        : '#' ~('#')+ '#' | '\'' [0-9]+ '/' [0-9]+ '/' [0-9]+ '\'' ;
INTEGER_LITERAL     : [+-]? [0-9]+ ;
DECIMAL_LITERAL     : [+-]? [0-9]* '.' [0-9]+ ([eE] [+-]? [0-9]+)? ;
BOOLEAN_LITERAL     : TRUE | FALSE ;
NULL_LITERAL        : NULL ;

// Identifiers
IDENTIFIER          : [a-zA-Z_] [a-zA-Z0-9_]* ;
BRACKETED_IDENTIFIER : '[' (~']')* ']' ;

// Whitespace
WS                  : [ \t\r\n]+ -> skip ;
