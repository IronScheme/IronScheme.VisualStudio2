
(library (visualstudio)
  (export 
    read-file
    read-imports)
  (import 
    (ironscheme)
    (ironscheme reader)
    (ironscheme clr))

  (define (read-file port)
    (let f ((a '()))
      (let ((e (read-annotated port)))
        (if (eof-object? e)
            (reverse a)
            (cons e a)))))
            
  (define (read-imports content)
    '(rnrs))      
            
)
