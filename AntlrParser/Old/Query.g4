grammar Query;

// Parser rules
query
    : expression EOF            # ExpressionQuery
    | function EOF              # FunctionQuery
    ;

expression
    : literal                   # LiteralExpression
    | columnName                # ColumnExpression
    | parentChildExpression     # ParentChildExpr
    | function                  # FunctionExpression
    | '(' expression ')'        # ParenExpression
    | 'NOT' expression          # NotExpression
    | expression op=('AND' | 'OR') expression # LogicalExpression
    | expression op=('+' | '-' | '*' | '/' | '%') expression # ArithmeticExpression
    | expression op=('=' | '<>' | '>' | '<' | '>=' | '<=') expression # BinaryCondition
    | expression 'LIKE' STRING_LITERAL          # LikeCondition
    | expression 'IS' 'NULL'                    # IsNullCondition
    | expression 'IS' 'NOT' 'NULL'              # IsNotNullCondition
    | expression 'IN' '(' literalList? ')'      # InCondition
    | expression 'BETWEEN' expression 'AND' expression # BetweenCondition
    ;

function
    : 'CONVERT' '(' expression ',' type ')'     # ConvertFunction
    | 'LEN' '(' expression ')'                  # LenFunction
    | 'ISNULL' '(' expression ',' expression ')' # IsNullFunction
    | 'IIF' '(' expression ',' expression ',' expression ')' # IifFunction
    | 'TRIM' '(' expression ')'                 # TrimFunction
    | 'SUBSTRING' '(' expression ',' expression ',' expression ')' # SubstringFunction
    | aggregate '(' parentChildExpression ')'   # AggregateFunction
    ;

type
    : STRING_LITERAL
    ;

aggregate
    : 'SUM' | 'AVG' | 'MIN' | 'MAX' | 'COUNT' | 'STDEV' | 'VAR'
    ;

parentChildExpression
    : relationColumn
    ;

relationColumn
    : ('PARENT' '.' | 'CHILD' '.')? columnName ('.' columnName)*
    ;

literalList
    : literal (',' literal)*
    ;

literal
    : STRING_LITERAL
    | INTEGER
    | DECIMAL
    | SCIENTIFIC
    | DATE_LITERAL
    | BOOLEAN
    | NULL
    ;

columnName
    : IDENTIFIER
    | BRACKETED_IDENTIFIER
    | GRAVE_IDENTIFIER
    ;

// Lexer rules
STRING_LITERAL: '\'' ( ~'\'' | '\'\'' )* '\'';
INTEGER: '-'? [0-9]+;
DECIMAL: '-'? [0-9]+ '.' [0-9]+;
SCIENTIFIC: '-'? [0-9]+ ('.' [0-9]+)? [eE] [+-]? [0-9]+;
DATE_LITERAL: '#' ~'#'+ '#';
BOOLEAN: 'true' | 'false';
NULL: 'NULL';
IDENTIFIER: [a-zA-Z_] [a-zA-Z0-9_]*;
BRACKETED_IDENTIFIER: '[' ( ~']' | '\\]' )+ ']';
GRAVE_IDENTIFIER: '`' ~'`'+ '`';
WHITESPACE: [ \t\r\n]+ -> skip;