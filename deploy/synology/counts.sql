SELECT 'Categories' AS entity, count(*) AS rows FROM "Categories"
UNION ALL SELECT 'Recipes', count(*) FROM "Recipes"
UNION ALL SELECT 'Stages', count(*) FROM "Stages"
UNION ALL SELECT 'Users', count(*) FROM "Users"
UNION ALL SELECT '__EFMigrationsHistory', count(*) FROM "__EFMigrationsHistory"
ORDER BY entity;
