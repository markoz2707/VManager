import React from 'react';

type ButtonVariant = 'primary' | 'secondary' | 'danger' | 'ghost' | 'toolbar';
type ButtonSize = 'sm' | 'md' | 'lg';

interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  size?: ButtonSize;
  leftIcon?: React.ReactNode;
}

export const Button: React.FC<ButtonProps> = ({ 
    variant = 'primary', 
    size = 'md', 
    leftIcon,
    children, 
    ...props 
}) => {
  const baseClasses = "flex items-center justify-center font-medium rounded focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-offset-gray-100 transition-colors duration-200 disabled:opacity-50 disabled:cursor-not-allowed border";

  const variantClasses = {
    primary: 'bg-primary hover:bg-primary-700 text-white border-primary-600 focus:ring-primary-500',
    secondary: 'bg-gray-200 hover:bg-gray-300 text-gray-800 border-gray-400 focus:ring-gray-500',
    danger: 'bg-red-600 hover:bg-red-700 text-white border-red-700 focus:ring-red-500',
    ghost: 'bg-transparent hover:bg-gray-200 text-gray-700 border-transparent focus:ring-gray-500',
    toolbar: 'bg-transparent hover:bg-gray-200 text-gray-700 border-transparent focus:ring-primary-500 disabled:text-gray-400',
  };

  const sizeClasses = {
    sm: 'px-2 py-1 text-xs',
    md: 'px-3 py-1.5 text-sm',
    lg: 'px-5 py-2.5 text-base',
  };
  
  const iconSizeClasses = {
      sm: 'h-4 w-4',
      md: 'h-5 w-5',
      lg: 'h-6 w-6'
  }

  return (
    <button
      className={`${baseClasses} ${variantClasses[variant]} ${sizeClasses[size]} ${props.className || ''}`}
      {...props}
    >
      {leftIcon && <span className={`mr-2 -ml-1 ${iconSizeClasses[size]}`}>{leftIcon}</span>}
      {children}
    </button>
  );
};