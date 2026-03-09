import http from 'k6/http';
import { check, sleep } from 'k6';

// 1. Configurações de Carga
export const options = {
    stages: [
        { duration: '30s', target: 5 }, // Pequena carga para validar o fluxo
        { duration: '1m', target: 5 },
        { duration: '30s', target: 0 },
    ],
    thresholds: {
        http_req_failed: ['rate<0.05'], // Permitimos até 5% de erro para fluxos complexos
        http_req_duration: ['p(95)<1000'], // 95% abaixo de 1s
    },
};

const BASE_URL = 'http://localhost:5266/api/v1';

// Função auxiliar para gerar XML com Chave de Acesso única
function generateXml(key) {
    return `<?xml version="1.0" encoding="utf-8"?>
<nfeProc xmlns="http://www.portalfiscal.inf.br/nfe">
    <NFe>
        <infNFe Id="NFe${key}">
            <ide><mod>55</mod><nNF>1</nNF><serie>1</serie><dhEmi>2023-01-01T10:00:00-03:00</dhEmi></ide>
            <emit><CNPJ>07689002000189</CNPJ><xNome>EMBRAER</xNome><enderEmit><UF>SP</UF></enderEmit></emit>
            <dest><xNome>DEST K6</xNome></dest>
            <total><ICMSTot><vNF>100.00</vNF></ICMSTot></total>
            <det nItem="1"><prod><cProd>P1</cProd><xProd>PROD K6</xProd><CFOP>7501</CFOP><uCom>UN</uCom><qCom>1</qCom><vUnCom>100</vUnCom><vProd>100</vProd></prod></det>
        </infNFe>
    </NFe>
</nfeProc>`;
}

export default function () {
    // A. GERAR DADOS ÚNICOS
    const randomKey = Array.from({length: 44}, () => Math.floor(Math.random() * 10)).join('');
    const xmlContent = generateXml(randomKey);

    const headers = { 'Content-Type': 'application/xml' };

    // 1. POST - CRIAR DOCUMENTO
    const postRes = http.post(`${BASE_URL}/fiscal-documents/xml`, xmlContent, { headers });
    
    const created = check(postRes, {
        'create: status is 201': (r) => r.status === 201,
        'create: has id': (r) => r.json().id !== undefined,
    });

    if (created) {
        const docId = postRes.json().id;

        // 2. GET - CONSULTAR DETALHES
        const getRes = http.get(`${BASE_URL}/fiscal-documents/${docId}`);
        check(getRes, {
            'get: status is 200': (r) => r.status === 200,
            'get: id matches': (r) => r.json().id === docId,
        });

        // 3. DELETE - EXCLUIR
        const delRes = http.del(`${BASE_URL}/fiscal-documents/${docId}`);
        check(delRes, {
            'delete: status is 200': (r) => r.status === 200,
        });
    }

    sleep(1);
}
