
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
            (f (cons e a))))))
            
  (define (read-imports content)
    (let ((c (car content)))
      (if (= 1 (length content))
          (cdr (cadddr (annotation-stripped c)))
          (cdr (annotation-stripped c)))))
            
)
