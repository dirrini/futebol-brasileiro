import assert from 'node:assert/strict';
import test from 'node:test';
import { MAX_BYTES, parseDocument } from '../server/strict-json.mjs';

const invalid = text => assert.throws(() => parseDocument(text), error => error.status === 422 && error.code === 'invalid_json');

test('strict JSON preserves valid Unicode, integer values and escaped strings', () => {
  assert.deepEqual(parseDocument(' {"name":"São Paulo \\uD83D\\uDE00","quote":"a\\\"b","slash":"a\\\\b","n":-12,"flag":true,"empty":null} '),
    { name: 'São Paulo 😀', quote: 'a"b', slash: 'a\\b', n: -12, flag: true, empty: null });
});

test('strict JSON rejects duplicate keys including equivalent escaped spellings', () => {
  invalid('{"schemaVersion":1,"schemaVersion":2}');
  invalid('{"name":"one","na\\u006de":"two"}');
  invalid('{"nested":{"x":1,"x":2}}');
});

test('strict JSON rejects decimal and exponential spellings despite integer numeric value', () => {
  for (const number of ['1.0', '1e0', '1E+0', '-2.00', '0.25']) invalid(`{"n":${number}}`);
});

test('strict JSON rejects JavaScript extensions, malformed syntax and extra documents', () => {
  for (const source of ['', ' ', "{'name':'x'}", '{name:1}', '{"a":1,}', '[1,]', '{"a":01}',
    '{"a":NaN}', '{"a":Infinity}', '{"a":undefined}', '{/*comment*/"a":1}', '{}{}', '[] 0',
    '{"a":"bad\\q"}', '{"a":"bad\nstring"}', '{"a":"unterminated}', '[1', '{"a":truefalse}']) invalid(source);
});

test('strict JSON rejects unpaired UTF-16 surrogates and accepts complete pairs', () => {
  for (const source of ['"\\uD800"', '"\\uDC00"', '"\\uD800\\u0041"', '"\\uD800\\uD800"',
    '"' + String.fromCharCode(0xd800) + '"', '"' + String.fromCharCode(0xdc00) + '"']) invalid(source);
  assert.equal(parseDocument('"\\uD83D\\uDE00"'), '😀');
});

test('strict JSON counts containers consistently with Unity at the depth boundary', () => {
  assert.doesNotThrow(() => parseDocument('['.repeat(32) + ']'.repeat(32)));
  assert.doesNotThrow(() => parseDocument('['.repeat(32) + '0' + ']'.repeat(32)));
  invalid('['.repeat(33) + ']'.repeat(33));
  invalid('['.repeat(33) + '0' + ']'.repeat(33));
});

test('strict JSON limits UTF-8 bytes rather than JavaScript string length', () => {
  assert.equal(parseDocument('"' + 'a'.repeat(MAX_BYTES - 2) + '"').length, MAX_BYTES - 2);
  invalid('"' + 'a'.repeat(MAX_BYTES - 1) + '"');
  invalid('"' + 'é'.repeat(MAX_BYTES / 2) + '"');
});
