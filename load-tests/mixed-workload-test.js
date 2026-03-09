import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
    stages: [
        { duration: '20s', target: 10 },
        { duration: '40s', target: 10 },
        { duration: '20s', target: 0 },
    ],
    thresholds: {
        http_req_duration: ['p(95)<500'],
        http_req_failed: ['rate<0.01'],
    },
};

const BASE_URL = 'http://localhost:5266/api/v1/fiscal-documents';

function generateXml(key) {
    return `<nfeProc xmlns="http://www.portalfiscal.inf.br/nfe"><NFe><infNFe Id="NFe${key}"><ide><mod>55</mod><nNF>1</nNF><serie>1</serie><dhEmi>2023-01-01T10:00:00-03:00</dhEmi></ide><emit><CNPJ>07689002000189</CNPJ><xNome>MIXED</xNome><enderEmit><UF>SP</UF></enderEmit></emit><dest><xNome>DEST</xNome></dest><total><ICMSTot><vNF>10.00</vNF></ICMSTot></total><det nItem="1"><prod><cProd>P</cProd><xProd>P</xProd><CFOP>7501</CFOP><uCom>UN</uCom><qCom>1</qCom><vUnCom>10</vUnCom><vProd>10</vProd></prod></det></infNFe></NFe></nfeProc>`;
}

export default function () {
    const rand = Math.random();

    if (rand < 0.2) {
        // 20% Upload
        const key = Array.from({length: 44}, () => Math.floor(Math.random() * 10)).join('');
        const res = http.post(`${BASE_URL}/xml`, generateXml(key), { headers: { 'Content-Type': 'application/xml' } });
        check(res, { 'upload success': (r) => r.status === 201 });
    } 
    else if (rand < 0.8) {
        // 60% Listagem com Filtros
        const res = http.get(`${BASE_URL}?uf=SP&page=1&pageSize=10`);
        check(res, { 'list success': (r) => r.status === 200 });
    } 
    else {
        // 20% Leitura de XML Bruto (Sub-recurso)
        // Primeiro lista para pegar um ID
        const list = http.get(`${BASE_URL}?pageSize=1`);
        const items = list.json().items;
        if (items && items.length > 0) {
            const res = http.get(`${BASE_URL}/${items[0].id}/xml`);
            check(res, { 'get xml success': (r) => r.status === 200 });
        }
    }

    sleep(1);
}
