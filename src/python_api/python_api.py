from fastapi import FastAPI, HTTPException, UploadFile, File
from fastapi.responses import JSONResponse
import fitz  # PyMuPDF

app = FastAPI(
    title="PDF Reader API",
    description="API for reading PDF files using PyMuPDF"
)

# Maximum file size: 100MB
MAX_FILE_SIZE = 100 * 1024 * 1024


@app.get('/')
async def root():
    """Root endpoint with API information"""
    return {
        'message': 'PDF Reader API',
        'endpoints': {
            '/read-pdf': 'POST - Extract text from uploaded PDF file',
            '/health': 'GET - Health check',
            '/docs': 'GET - Interactive API documentation (Swagger UI)',
            '/redoc': 'GET - Alternative API documentation (ReDoc)'
        },
        'usage': 'Access the API at http://localhost:5000 or http://127.0.0.1:5000'
    }


@app.post('/pymupdf/read-pdf')
async def read_pdf(file: UploadFile = File(...)):
    """
    Extracts text from an uploaded PDF file and returns it.
    
    Args:
        file: PDF file to process (multipart/form-data)
    
    Returns:
        JSON response with extracted text, page count, and filename
    """
    try:
        # Validate file type
        if not file.filename:
            raise HTTPException(
                status_code=400,
                detail={
                    'error': 'No file provided',
                    'success': False
                }
            )
        
        # Check file extension
        if not file.filename.lower().endswith('.pdf'):
            raise HTTPException(
                status_code=400,
                detail={
                    'error': f'Invalid file type. Expected PDF, got: {file.filename}',
                    'success': False
                }
            )
        
        # Check content type if provided
        if file.content_type and file.content_type != 'application/pdf':
            raise HTTPException(
                status_code=400,
                detail={
                    'error': f'Invalid content type. Expected application/pdf, got: {file.content_type}',
                    'success': False
                }
            )
        
        # Read file content
        file_bytes = await file.read()
        
        # Validate file size
        file_size = len(file_bytes)
        if file_size > MAX_FILE_SIZE:
            raise HTTPException(
                status_code=413,
                detail={
                    'error': f'File too large. Maximum size is {MAX_FILE_SIZE / (1024 * 1024):.0f}MB, got {file_size / (1024 * 1024):.2f}MB',
                    'success': False
                }
            )
        
        if file_size == 0:
            raise HTTPException(
                status_code=400,
                detail={
                    'error': 'File is empty',
                    'success': False
                }
            )
        
        # Open PDF from bytes
        try:
            doc = fitz.open(stream=file_bytes, filetype="pdf")
        except Exception as e:
            raise HTTPException(
                status_code=400,
                detail={
                    'error': f'Invalid or corrupted PDF file: {str(e)}',
                    'success': False
                }
            )
        
        text_content = []
        
        # Extract text from each page
        try:
            for page_num in range(doc.page_count):
                page = doc[page_num]
                text = page.get_text()
                if text.strip():  # Only add non-empty pages
                    text_content.append({
                        'page': page_num + 1,  # 1-indexed for display
                        'text': text
                    })
        finally:
            doc.close()
        
        # Combine all text
        full_text = '\n\n'.join([page['text'] for page in text_content])
        
        return {
            'success': True,
            'text': full_text,
            'pages': len(text_content),
            'filename': file.filename
        }
    
    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(
            status_code=500,
            detail={
                'error': f'Error processing PDF: {str(e)}',
                'success': False
            }
        )


@app.get('/health')
async def health():
    """Health check endpoint"""
    return {'status': 'ok'}


if __name__ == '__main__':
    import uvicorn
    # Run the FastAPI app with Uvicorn
    uvicorn.run(app, host='0.0.0.0', port=5000, reload=True)

