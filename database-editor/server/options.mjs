const labels = {
  skinTone: ['Tom 1', 'Tom 2', 'Tom 3', 'Tom 4', 'Tom 5', 'Tom 6'],
  hairStyle: ['Sem cabelo', 'Curto', 'Estilizado', 'Estilizado 2', 'Moicano', 'Locs', 'Curto repartido'],
  hairColor: ['Castanho', 'Castanho escuro', 'Preto', 'Loiro claro', 'Loiro', 'Cinza', 'Branco', 'Verde', 'Verde escuro', 'Azul', 'Azul escuro', 'Vermelho claro', 'Vermelho', 'Laranja claro', 'Laranja'],
  beardStyle: ['Sem barba', 'Bigode', 'Cavanhaque', 'Barba longa'],
  bootsColor: ['Preta', 'Rosa', 'Laranja', 'Roxa', 'Azul-celeste', 'Cinza', 'Branca'],
  sockAccessoryColor: ['Nenhuma', 'Preta', 'Cinza', 'Branca']
};
labels.beardColor = labels.hairColor;
// Illustrative swatches sampled from the current Unity palettes; the game owns rendering.
const colors = {
  skinTone: ['#F3BF8D', '#D9A67A', '#BF8D68', '#A57455', '#604231', '#1F1410'],
  hairColor: ['#572000', '#2B1000', '#1D1916', '#DBC88D', '#E5B748', '#8C8B8B', '#D1D1D1', '#59A837', '#0C590E', '#3C9EB7', '#0F648E', '#F86F7F', '#8E1A28', '#E58359', '#873715'],
  bootsColor: ['#000000', '#FF239B', '#FF3600', '#DC00FF', '#00D1FF', '#A6A6A6', '#FFFFFF'],
  sockAccessoryColor: [null, '#000000', '#878787', '#FFFFFF']
};
colors.beardColor = colors.hairColor;
const fields = {
  skinTone: 'Cor de pele', hairStyle: 'Tipo de cabelo', hairColor: 'Cor do cabelo',
  beardStyle: 'Tipo de barba', beardColor: 'Cor da barba', bootsColor: 'Chuteiras', sockAccessoryColor: 'Faixa da meia'
};
const attributeLabels = {
  strength: 'Força', acceleration: 'Aceleração', topSpeed: 'Velocidade máxima', dribbleSpeed: 'Velocidade com bola',
  jump: 'Impulsão', tackling: 'Desarme', ballKeeping: 'Proteção de bola', passing: 'Passe', longBall: 'Lançamento',
  agility: 'Agilidade', shooting: 'Finalização', shootPower: 'Potência do chute', positioning: 'Posicionamento', reaction: 'Reação', ballControl: 'Controle de bola'
};
const positionLabels = { GK: 'Goleiro', RB: 'Lateral direito', LB: 'Lateral esquerdo', CB: 'Zagueiro', DM: 'Volante', CM: 'Meia central', RM: 'Meia direito', LM: 'Meia esquerdo', AM: 'Meia ofensivo', LW: 'Ponta esquerda', RW: 'Ponta direita', ST: 'Atacante' };

export function createOptions(schema) {
  const definitions = schema.definitions;
  const appearance = definitions.builtinAppearance ?? definitions.appearance;
  if (!appearance) throw new Error('The v2 schema must define built-in appearance.');
  return {
    appearance: Object.entries(fields).map(([field, label]) => ({ field, label,
      options: appearance.properties[field].enum.map((value, index) => ({ value, label: labels[field][index], ...(colors[field] ? { color: colors[field][index] } : {}) })) })),
    positions: Object.entries(positionLabels).map(([value, label]) => ({ value, label })),
    attributes: Object.entries(attributeLabels).map(([field, label]) => ({ field, label })),
    limits: { heightCm: { min: 150, max: 210 }, weightKg: { min: 45, max: 100 }, attributes: { min: 0, max: 100 } },
    defaultAppearance: { skinTone: 'tone-3', hairStyle: 'short', hairColor: 'black', beardStyle: 'none', beardColor: 'black', bootsColor: 'black', sockAccessoryColor: 'none' }
  };
}
