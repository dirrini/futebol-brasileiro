import { readFile } from 'node:fs/promises';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { DatabaseStore } from './database-store.mjs';
import { createValidator } from './validation.mjs';
import { createEditorServer } from './http-server.mjs';
import { createOptions } from './options.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const schemaRoot = process.env.SCHEMA_DIRECTORY ?? resolve(root, '../Assets/FootballSimulator/Data/FootballWorld/Schemas');
const schemas = await Promise.all([1, 2, 3].map(async version => JSON.parse(await readFile(resolve(schemaRoot, `database-v${version}.schema.json`), 'utf8'))));
const store = new DatabaseStore(process.env.DATABASE_PATH ?? resolve(root, '../Assets/FootballSimulator/Data/FootballWorld/Examples/four-clubs.database.json'), createValidator(schemas));
// Absolute URN refs preserve each source schema's local definitions in this endpoint.
const schema = { $schema: 'http://json-schema.org/draft-07/schema#', definitions: Object.fromEntries(schemas.map(source => [`v${source.properties.schemaVersion.const}`, source])), oneOf: schemas.map(source => ({ $ref: source.$id })) };
const server = createEditorServer({ store, options: createOptions(schemas[1]), schema, clientRoot: resolve(root, 'client'),
  allowedOrigins: (process.env.ALLOWED_ORIGINS ?? 'http://localhost:8080,http://127.0.0.1:8080').split(',') });
server.listen(Number(process.env.PORT ?? 3000), process.env.HOST ?? '0.0.0.0', () => console.log('Database editor ready.'));
