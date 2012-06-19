
(library (visualstudio)
  (export 
    read-file
    get-forms
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

  (define (get-forms proc)
    (let-values (((p e) (open-string-output-port)))
      (for-each (lambda (f)
                  (fprintf p "~a\n" f))
                (call-with-values (lambda () (procedure-form proc)) list))
      (e)))
            
  (define (read-imports content)
    (let ((c (car content)))
      (if (= 1 (length content))
          (cdr (cadddr (annotation-stripped c)))
          (cdr (annotation-stripped c)))))
            
)
