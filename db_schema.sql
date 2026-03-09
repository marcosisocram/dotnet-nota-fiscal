CREATE TABLE fiscaldocuments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    accesskey VARCHAR(44) NOT NULL,
    documenttype INT NOT NULL,
    documentnumber VARCHAR(20) NOT NULL, 
    series VARCHAR(10) NOT NULL,         
    emissiondate TIMESTAMPTZ NOT NULL, 
    emittercnpjcpf VARCHAR(14) NOT NULL, 
    emittername VARCHAR(255) NOT NULL,  
    emitterstate CHAR(2) NOT NULL,       
    receivercnpjcpf VARCHAR(14) NULL,    
    receivername VARCHAR(255) NOT NULL, 
    receiverstate CHAR(2) NULL,          
    totalvalue DECIMAL(18, 4) NOT NULL,  
    rawxml TEXT NOT NULL,
    createdat TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updatedat TIMESTAMPTZ NULL,

    CONSTRAINT uq_fiscaldocuments_accesskey UNIQUE (accesskey)
);

CREATE TABLE fiscaldocumentitems (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    fiscaldocumentid UUID NOT NULL,
    itemnumber INT NOT NULL,              
    productcode VARCHAR(60) NOT NULL,     
    productname VARCHAR(255) NOT NULL,   
    ncm VARCHAR(10) NULL,                 
    cfop VARCHAR(4) NOT NULL,             
    unit VARCHAR(10) NOT NULL,            
    quantity DECIMAL(18, 4) NOT NULL,     
    unitprice DECIMAL(18, 10) NOT NULL,   
    totalprice DECIMAL(18, 4) NOT NULL,   
    
    CONSTRAINT fk_fiscaldocumentitems_fiscaldocuments 
        FOREIGN KEY (fiscaldocumentid) REFERENCES fiscaldocuments(id) ON DELETE CASCADE
);

CREATE INDEX ix_fiscaldocuments_emissiondate ON fiscaldocuments(emissiondate);
CREATE INDEX ix_fiscaldocuments_emittercnpjcpf ON fiscaldocuments(emittercnpjcpf);
CREATE INDEX ix_fiscaldocuments_receivercnpjcpf ON fiscaldocuments(receivercnpjcpf);
CREATE INDEX ix_fiscaldocuments_emitterstate ON fiscaldocuments(emitterstate);
