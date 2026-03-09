import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
    stages: [
        { duration: '10s', target: 5 },
        { duration: '20s', target: 5 },
        { duration: '10s', target: 0 },
    ],
    thresholds: {
        // http_req_failed deve ser baixo, mas note que o status 409 é esperado aqui.
        // Por padrão, k6 considera > 399 como falha se não filtrarmos.
        'http_req_failed{status:409}': ['rate>=0'], 
    },
};

const BASE_URL = 'http://localhost:5266/api/v1';

function generateXml(key) {
    return `<?xml version="1.0" encoding="utf-8"?>
<nfeProc xmlns="http://www.portalfiscal.inf.br/nfe">
    <NFe>
        <infNFe Id="NFe${key}">
            <ide><mod>55</mod><nNF>1</nNF><serie>1</serie><dhEmi>2023-01-01T10:00:00-03:00</dhEmi></ide>
            <emit><CNPJ>07689002000189</CNPJ><xNome>EMBRAER</xNome><enderEmit><UF>SP</UF></enderEmit></emit>
            <dest><xNome>DEST IDEMPOTENCIA</xNome></dest>
            <total><ICMSTot><vNF>100.00</vNF></ICMSTot></total>
            <det nItem="1"><prod><cProd>P1</cProd><xProd>PROD</xProd><CFOP>7501</CFOP><uCom>UN</uCom><qCom>1</qCom><vUnCom>100</vUnCom><vProd>100</vProd></prod></det>
        </infNFe>
    </NFe>
</nfeProc>`;
}

export default function () {
    const randomKey = Array.from({length: 44}, () => Math.floor(Math.random() * 10)).join('');
    const xmlContent = generateXml(randomKey);
    const headers = { 'Content-Type': 'application/xml' };

    // 1. PRIMEIRO CADASTRO (Sucesso)
    const res1 = http.post(`${BASE_URL}/fiscal-documents/xml`, xmlContent, { headers });
    const isCreated = check(res1, {
        'step 1: first upload is 201': (r) => r.status === 201,
    });

    if (isCreated) {
        const docId = res1.json().id;

        // 2. SEGUNDO CADASTRO (Conflito/Duplicado)
        const res2 = http.post(`${BASE_URL}/fiscal-documents/xml`, xmlContent, { headers });
        check(res2, {
            'step 2: second upload is 409': (r) => r.status === 409,
            'step 2: error message is correct': (r) => r.json().message === 'Documento já processado.',
        });

        // 3. DELETAR (Limpeza)
        const res3 = http.del(`${BASE_URL}/fiscal-documents/${docId}`);
        check(res3, {
            'step 3: delete is 200': (r) => r.status === 200,
        });
    }

    sleep(1);
}
